#!/usr/bin/env python3
"""Generate src/RegionsOfXIV/Localization/en.json from the Loc call sites.

The English a player sees is compiled into the call sites -- Loc.Get(key, english) -- and en.json
exists only so translators have something to work from. Transcribing it by hand let the two drift,
so it is generated instead.

What is carried over from the existing file rather than derived:

  * description fields, which are hand-written notes to translators and the most valuable thing
    in the file. A key that loses its description is reported rather than silently blanked.
  * the order keys appear in, and the blank lines between groups, so regenerating a file nothing
    has changed in produces no diff at all.

    python tools/export-en-json.py            write the file
    python tools/export-en-json.py --check    report what would change, write nothing
"""
import io
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "src", "RegionsOfXIV")
TARGET = os.path.join(SOURCE, "Localization", "en.json")

CALLS = ["Loc.Get(", "Loc.Label(", "Loc.Format(", "Loc.Unit("]

ESCAPES = {"n": "\n", "t": "\t", "r": "\r", '"': '"', "\\": "\\", "0": "\0", "'": "'"}


def read_literal(s, i):
    assert s[i] == '"'
    i += 1
    out = []
    while True:
        c = s[i]
        if c == "\\":
            nxt = s[i + 1]
            if nxt not in ESCAPES:
                raise ValueError("unhandled escape \\%s near %r" % (nxt, s[i - 40:i + 20]))
            out.append(ESCAPES[nxt])
            i += 2
        elif c == '"':
            return "".join(out), i + 1
        else:
            out.append(c)
            i += 1


def skip_space(s, i):
    while i < len(s) and s[i] in " \t\r\n":
        i += 1
    return i


def read_concat(s, i):
    """A run of string literals joined by '+', as the call sites wrap long English."""
    i = skip_space(s, i)
    if i >= len(s) or s[i] != '"':
        return None, i
    parts = []
    while True:
        value, i = read_literal(s, i)
        parts.append(value)
        j = skip_space(s, i)
        if j < len(s) and s[j] == "+":
            j = skip_space(s, j + 1)
            if j < len(s) and s[j] == '"':
                i = j
                continue
        return "".join(parts), i


def scan():
    """key -> {english, sites}. Calls whose English is not a literal are reported separately."""
    found = {}
    dynamic = []

    for folder, _, names in os.walk(SOURCE):
        for name in sorted(names):
            if not name.endswith(".cs"):
                continue
            path = os.path.join(folder, name)
            rel = os.path.relpath(path, ROOT).replace("\\", "/")
            src = io.open(path, encoding="utf-8-sig").read()

            for call in CALLS:
                at = 0
                while True:
                    at = src.find(call, at)
                    if at < 0:
                        break
                    i = skip_space(src, at + len(call))
                    at += len(call)
                    if i >= len(src) or src[i] != '"':
                        continue
                    key, i = read_literal(src, i)
                    i = skip_space(src, i)
                    if i >= len(src) or src[i] != ",":
                        continue
                    english, _ = read_concat(src, i + 1)
                    if english is None:
                        dynamic.append((rel, key))
                        continue
                    found.setdefault(key, {"english": english, "sites": []})
                    if found[key]["english"] != english:
                        raise SystemExit(
                            "%s is used with two different English strings:\n  %r\n  %r"
                            % (key, found[key]["english"], english))
                    found[key]["sites"].append(rel)

    return found, dynamic


def read_existing():
    """Descriptions, messages, key order, and which keys had a blank line before them."""
    if not os.path.exists(TARGET):
        return {}, {}, [], set(), None

    text = io.open(TARGET, encoding="utf-8", newline="").read()
    table = json.loads(text)

    order = [k for k in table if not k.startswith("_")]
    descriptions = {k: v.get("description", "") for k, v in table.items()
                    if not k.startswith("_") and isinstance(v, dict)}
    messages = {k: v.get("message", "") for k, v in table.items()
                if not k.startswith("_") and isinstance(v, dict)}

    spaced = set()
    lines = text.split("\n")
    for n, line in enumerate(lines):
        match = re.match(r'  "([^"]+)": \{', line)
        if match and n > 0 and lines[n - 1].strip() == "":
            spaced.add(match.group(1))

    return descriptions, messages, order, spaced, table.get("_note")


def render(keys, english, descriptions, spaced, note):
    out = ["{"]
    if note:
        out.append('  "_note": %s,' % json.dumps(note, ensure_ascii=False))
        out.append("")

    for n, key in enumerate(keys):
        if n > 0 and key in spaced:
            out.append("")
        out.append('  %s: {' % json.dumps(key, ensure_ascii=False))
        out.append('    "message": %s,' % json.dumps(english[key], ensure_ascii=False))
        out.append('    "description": %s'
                   % json.dumps(descriptions.get(key, ""), ensure_ascii=False))
        out.append("  },")

    out[-1] = out[-1][:-1]  # the last entry takes no trailing comma
    out.append("}")
    return "\n".join(out) + "\n"


def main():
    check = "--check" in sys.argv

    found, dynamic = scan()
    descriptions, messages, order, spaced, note = read_existing()

    # A handful of call sites pass a variable rather than a literal -- the built-in preset
    # descriptions come off the Preset record. Their English cannot be derived, so the entry
    # already in the file is carried through rather than dropped, and they are listed below so a
    # stale one is at least visible.
    carried = {key for _, key in dynamic if key in messages}

    english = {k: v["english"] for k, v in found.items()}
    for key in carried:
        english.setdefault(key, messages[key])

    known = set(english)

    kept = [k for k in order if k in known]
    added = sorted(k for k in known if k not in order)
    dropped = [k for k in order if k not in known]

    # New keys land at the end, grouped by their first segment, for a human to file properly.
    for key in sorted(added, key=lambda k: (k.split(".")[0], k)):
        kept.append(key)

    undescribed = [k for k in kept if not descriptions.get(k)]

    text = render(kept, english, descriptions, spaced, note)
    current = io.open(TARGET, encoding="utf-8", newline="").read() if os.path.exists(TARGET) else None

    print("call sites  : %d keys" % len(found))
    for label, keys in (("added", added), ("dropped", dropped), ("no description", undescribed)):
        if keys:
            print("%-12s: %d" % (label, len(keys)))
            for k in keys:
                print("    %s" % k)
    if carried:
        print("english not a literal, carried from the existing file: %d" % len(carried))
        for key in sorted(carried):
            print("    %s" % key)

    orphaned = sorted({key for _, key in dynamic} - carried)
    if orphaned:
        print("english not a literal AND no existing entry: %d" % len(orphaned))
        for key in orphaned:
            print("    %s" % key)

    if text == current:
        print("\nen.json is already exactly what the call sites say.")
        return 0

    if check:
        print("\nen.json is OUT OF DATE. Run without --check to rewrite it.")
        return 1

    io.open(TARGET, "w", encoding="utf-8", newline="\n").write(text)
    print("\nwrote %s (%d entries)" % (os.path.relpath(TARGET, ROOT), len(kept)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
