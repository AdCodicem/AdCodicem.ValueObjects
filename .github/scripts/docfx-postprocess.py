#!/usr/bin/env python3
"""Make DocFX's markdown output safe for Docusaurus.

DocFX writes heading anchors as raw HTML:

    ### <a id="AdCodicem_ValueObjects_ValidationResult_ErrorCode"></a> ErrorCode

Docusaurus then wraps every heading in its own anchor link, producing nested
<a> elements -- invalid HTML that the build's minifier reports several hundred
times. Docusaurus has explicit heading ids for exactly this, so rewrite to:

    ### ErrorCode {#AdCodicem_ValueObjects_ValidationResult_ErrorCode}

The id is preserved verbatim, so the cross-references DocFX generates between
pages keep resolving.
"""
import pathlib
import re
import sys

HEADING_ANCHOR = re.compile(
    r'^(?P<hashes>#{1,6}) <a id="(?P<id>[^"]+)"></a>[ ]*(?P<title>.*)$',
    re.MULTILINE,
)


def rewrite(text: str) -> tuple[str, int]:
    count = 0

    def replace(match: re.Match[str]) -> str:
        nonlocal count
        title = match["title"].strip()
        if not title:
            # A heading with no text left would render as an empty anchor; leave
            # these alone rather than emitting '## {#id}'.
            return match[0]
        count += 1
        return f'{match["hashes"]} {title} {{#{match["id"]}}}'

    return HEADING_ANCHOR.sub(replace, text), count


def main(directory: str) -> int:
    root = pathlib.Path(directory)
    if not root.is_dir():
        print(f"error: {root} is not a directory", file=sys.stderr)
        return 1

    files = sorted(root.rglob("*.md"))
    if not files:
        print(f"error: no markdown found under {root}", file=sys.stderr)
        return 1

    total = 0
    for path in files:
        original = path.read_text(encoding="utf-8")
        rewritten, count = rewrite(original)
        if count:
            path.write_text(rewritten, encoding="utf-8")
            total += count

    print(f"Rewrote {total} heading anchors across {len(files)} files.")

    remaining = sum(
        len(re.findall(r'^#{1,6} <a id=', p.read_text(encoding="utf-8"), re.MULTILINE))
        for p in files
    )
    if remaining:
        print(f"error: {remaining} heading anchors were left unconverted", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1] if len(sys.argv) > 1 else "website/docs/api"))
