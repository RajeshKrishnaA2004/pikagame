"""Structural sanity check for the C# files added in Stage 1.

Brace counting alone is not enough: a balanced file can still contain
statements stranded at class-body depth, which is exactly the bug that broke
GridPlayerController.cs (CS1519 / CS8803). This walks the files, strips
comments and string literals, tracks depth, and flags any line that sits at
depth 1 (i.e. directly inside a type) but is not a member declaration.

Run:  python Tools/check_cs_structure.py
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

FILES = [
    "Assets/Scripts/Player/GameInput.cs",
    "Assets/Scripts/Player/GridPlayerController.cs",
    "Assets/Scripts/Player/GridPlayerAnimator.cs",
    "Assets/Scripts/World/LockedCamera.cs",
    "Assets/Scripts/World/MapCollision.cs",
    "Assets/Scripts/World/NpcVisual.cs",
    "Assets/Scripts/World/ForegroundSort.cs",
    "Assets/Scripts/World/Inspectable.cs",
    "Assets/Scripts/World/PlayerInteraction.cs",
    "Assets/Scripts/Core/GameSave.cs",
    "Assets/Scripts/Core/GameSaveData.cs",
    "Assets/Scripts/UI/YesNoBox.cs",
    "Assets/Scripts/UI/MainMenuController.cs",
    "Assets/Scripts/Editor/PixelArtImporter.cs",
    "Assets/Scripts/Editor/ForestGen.cs",
    "Assets/Scripts/Editor/ChapterForest.cs",
    "Assets/Scripts/Editor/ChapterBuilder.cs",
]

# Tokens that legitimately start a member at class-body depth.
MEMBER_START = (
    "public", "private", "protected", "internal", "static", "sealed",
    "abstract", "partial", "class", "struct", "enum", "interface",
    "namespace", "using", "readonly", "const", "void", "event",
    "override", "virtual", "extern", "unsafe", "implicit", "explicit",
)

# Keywords that can ONLY begin a statement. Seeing one of these directly
# inside a type body means a method lost its closing brace.
STATEMENT_KEYWORDS = {
    "if", "else", "for", "foreach", "while", "do", "switch", "case",
    "default", "return", "break", "continue", "goto", "throw", "try",
    "catch", "finally", "lock", "yield", "checked", "unchecked", "new",
    "this", "base", "sizeof", "typeof", "nameof", "await", "throw",
}


def strip_code(line):
    """Remove comments and string/char literals so braces inside them are ignored."""
    out = []
    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        if c == '/' and i + 1 < n and line[i + 1] == '/':
            break                                    # line comment
        if c == '/' and i + 1 < n and line[i + 1] == '*':
            break                                    # block comment start
        if c == '@' and i + 1 < n and line[i + 1] == '"':
            i += 2
            while i < n:
                if line[i] == '"':
                    if i + 1 < n and line[i + 1] == '"':
                        i += 1
                    else:
                        break
                i += 1
        elif c == '"':
            i += 1
            while i < n and line[i] != '"':
                if line[i] == '\\':
                    i += 1
                i += 1
        elif c == "'":
            i += 1
            while i < n and line[i] != "'":
                if line[i] == '\\':
                    i += 1
                i += 1
        else:
            out.append(c)
        i += 1
    return "".join(out)


def check(path):
    full = os.path.join(ROOT, path)
    if not os.path.exists(full):
        return ["MISSING FILE: " + path]

    with open(full, encoding="utf-8") as fh:
        raw_lines = fh.read().splitlines()

    problems = []
    depth = 0
    in_block = False
    type_started = False
    type_ended = False
    prev_continues = False

    for no, raw in enumerate(raw_lines, start=1):
        line = raw

        # Track multi-line block comments so their braces are ignored.
        if in_block:
            if "*/" in line:
                line = line.split("*/", 1)[1]
                in_block = False
            else:
                continue

        while True:
            ci = line.find("/*")
            if ci < 0:
                break
            end = line.find("*/", ci + 2)
            if end < 0:
                line = line[:ci]
                in_block = True
                break
            line = line[:ci] + line[end + 2:]

        code = strip_code(line).strip()

        # Rule 1: nothing but members may sit directly inside a type body.
        # Skipped when this line merely continues the previous one (a wrapped
        # signature or a field initialiser such as "= new Dictionary<...>").
        is_continuation = prev_continues
        if code:
            prev_continues = code[-1] in "=(,:+?&|.><"

        if depth == 1 and code and not is_continuation:
            first = re.split(r"[\s(]", code, 1)[0]
            if first in STATEMENT_KEYWORDS:
                problems.append(
                    "line %d: '%s' is a STATEMENT at class-body depth - "
                    "a method is missing its closing brace" % (no, first))

        # Rule 2: once the type declaration closes, only further type
        # declarations may follow. This is the exact shape of the bug that
        # shipped in GridPlayerController.cs (CS8803).
        if type_ended and code:
            first = re.split(r"[\s(]", code, 1)[0]
            if first not in MEMBER_START:
                problems.append(
                    "line %d: '%s' appears AFTER the type declaration closed "
                    "(CS8803)" % (no, code[:60]))

        for ch in code:
            if ch == "{":
                depth += 1
                type_started = True
            elif ch == "}":
                depth -= 1
                if depth < 0:
                    problems.append("line %d: closing brace with depth 0" % no)
                    depth = 0
                elif depth == 0 and type_started:
                    type_ended = True

    if depth != 0:
        problems.append("file ends at depth %d (expected 0) - unbalanced braces" % depth)

    return problems


def main():
    failed = 0
    for path in FILES:
        problems = check(path)
        if problems:
            failed += 1
            print("FAIL  " + path)
            for p in problems:
                print("        " + p)
        else:
            print("ok    " + path)

    print("\n%d/%d files clean" % (len(FILES) - failed, len(FILES)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())