#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
정본 A~F(.docx) 의 run/문단 속성 자식 순서 검사 · 교정 도구.

    python tools/check_rpr_order.py [문서 ...] [--fix]

    [문서]  A B C D E F 중 일부 (기본: 전부)
    --fix   w:rPr · w:pPr 자식을 ECMA-376 순서로 재배열해 원본에 덮어쓴다

검사: word/document.xml, word/styles.xml 의 모든 <w:rPr> 에서
  - w:bCs 가 w:b 보다 앞에 오는 경우
  - w:iCs 가 w:i 보다 앞에 오는 경우
를 센다. <w:b/> 와 <w:b /> 가 섞여 있으므로 문자열이 아니라 lxml 로 자식 요소 순서를 비교한다.

--fix: 아래 RPR_ORDER / PPR_ORDER 순서로 정렬한다. 목록에 없는 요소는 원래 순서를 유지한 채 맨 뒤로 간다.
  재배열 전후로 파트별 문단 수와 문단 텍스트가 같은지 검증하고, 하나라도 다르면 저장하지 않고 중단한다.
  ⚠ D 는 CLAUDE.md §0-6 · §7 에 따라 승인 없이 편집하지 않는다.

종료 코드
  0  위반 없음 (--fix: 교정 · 검증 · 저장 완료)
  1  위반 있음 (--fix 없이 실행한 경우)
  2  파일 찾기 실패 · 검증 불일치 · 저장 실패로 중단
"""

import argparse
import glob
import io
import os
import sys
import tempfile
import zipfile

from lxml import etree

# 콘솔이 cp949 라 한글 파일명이 깨진다. read_canon.py 와 같은 처리.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except Exception:
        pass

# 정본 원본 위치 — read_canon.py 와 같다.
CANON_DIR = r"D:\낙원\file"
DOCS = ["A", "B", "C", "D", "E", "F"]
PARTS = ["word/document.xml", "word/styles.xml"]

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"

RPR_ORDER = ["rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps", "strike",
             "color", "spacing", "kern", "sz", "szCs", "u", "vertAlign", "rtl", "cs", "lang"]
PPR_ORDER = ["pStyle", "keepNext", "keepLines", "pageBreakBefore", "widowControl", "numPr",
             "pBdr", "shd", "tabs", "wordWrap", "spacing", "ind", "contextualSpacing", "jc", "outlineLvl",
             "rPr", "sectPr"]
ORDERS = {"rPr": RPR_ORDER, "pPr": PPR_ORDER}


def w(tag):
    return f"{{{W}}}{tag}"


def local_w(el):
    """w: 네임스페이스 요소면 로컬 이름, 아니면(주석·타 네임스페이스) None."""
    if not isinstance(el.tag, str):
        return None
    q = etree.QName(el)
    return q.localname if q.namespace == W else None


# ---------------------------------------------------------------------------
# 검사

def first_index(names, name):
    try:
        return names.index(name)
    except ValueError:
        return None


def count_violations(root):
    """(bCs 가 b 앞, iCs 가 i 앞) 인 rPr 개수."""
    b_bad = i_bad = 0
    for rpr in root.iter(w("rPr")):
        names = [local_w(ch) for ch in rpr]
        for strong, cs, is_b in (("b", "bCs", True), ("i", "iCs", False)):
            si, ci = first_index(names, strong), first_index(names, cs)
            if si is not None and ci is not None and ci < si:
                if is_b:
                    b_bad += 1
                else:
                    i_bad += 1
    return b_bad, i_bad


def count_unlisted(root):
    """rPr/pPr 안에서 순서 목록에 없는 자식 요소 — --fix 시 맨 뒤로 밀려난다."""
    found = {}
    for parent, order in ORDERS.items():
        for el in root.iter(w(parent)):
            for ch in el:
                if not isinstance(ch.tag, str):
                    continue
                name = local_w(ch)
                if name not in order:
                    key = f"{parent}/{name or etree.QName(ch).localname}"
                    found[key] = found.get(key, 0) + 1
    return found


# ---------------------------------------------------------------------------
# 교정

def reorder(root):
    """rPr·pPr 자식을 정렬한다. 순서가 바뀐 부모 요소 수를 돌려준다."""
    changed = 0
    for parent, order in ORDERS.items():
        rank = {name: i for i, name in enumerate(order)}
        tail = len(order)
        for el in root.iter(w(parent)):
            children = list(el)
            # sorted 는 안정 정렬이므로 목록 밖 요소끼리는 원래 순서가 유지된다.
            ordered = sorted(children, key=lambda ch: rank.get(local_w(ch), tail))
            if ordered != children:
                for ch in children:
                    el.remove(ch)
                el.extend(ordered)
                changed += 1
    return changed


def paragraph_snapshot(root):
    """파트 안의 문단별 텍스트 목록. 길이가 곧 문단 수다."""
    return ["".join(t.text or "" for t in p.iter(w("t"))) for p in root.iter(w("p"))]


def serialize(tree):
    return etree.tostring(tree, xml_declaration=True, encoding="UTF-8",
                          standalone=tree.docinfo.standalone)


def write_zip(path, replacements):
    """원본 zip 의 항목 순서 · 압축 방식을 유지한 채 지정 파트만 바꿔 쓴다."""
    fd, tmp = tempfile.mkstemp(suffix=".docx", dir=os.path.dirname(path))
    os.close(fd)
    try:
        with zipfile.ZipFile(path) as src, zipfile.ZipFile(tmp, "w") as dst:
            for info in src.infolist():
                data = replacements.get(info.filename)
                dst.writestr(info, data if data is not None else src.read(info.filename))
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.remove(tmp)
        raise


# ---------------------------------------------------------------------------

def find_doc(letter):
    hits = sorted(glob.glob(os.path.join(CANON_DIR, f"{letter}_*.docx")))
    if len(hits) != 1:
        raise SystemExit(f"[중단] {letter}_*.docx 가 {len(hits)}개다: {hits}")
    return hits[0]


class Abort(Exception):
    pass


def process(path, fix):
    """파일 하나를 검사(·교정)하고 위반 수를 돌려준다."""
    name = os.path.basename(path)
    with zipfile.ZipFile(path) as z:
        raw = {part: z.read(part) for part in PARTS}

    total = 0
    detail = []
    unlisted = {}
    replacements = {}
    reordered = 0
    for part in PARTS:
        # fromstring 은 XML 선언(standalone)을 잃으므로 parse 로 읽는다.
        tree = etree.parse(io.BytesIO(raw[part]))
        root = tree.getroot()

        b_bad, i_bad = count_violations(root)
        total += b_bad + i_bad
        detail.append(f"{part.split('/')[-1]} bCs<b {b_bad} · iCs<i {i_bad}")
        for k, v in count_unlisted(root).items():
            unlisted[k] = unlisted.get(k, 0) + v

        if fix:
            before = paragraph_snapshot(root)
            n = reorder(root)
            if n == 0:
                continue
            reordered += n
            data = serialize(tree)
            after_root = etree.fromstring(data)
            after = paragraph_snapshot(after_root)
            if len(before) != len(after):
                raise Abort(f"{name} {part}: 문단 수 {len(before)} → {len(after)}")
            if before != after:
                idx = next(i for i, (x, y) in enumerate(zip(before, after)) if x != y)
                raise Abort(f"{name} {part}: 문단 [{idx}] 텍스트 불일치")
            left = sum(count_violations(after_root))
            if left:
                raise Abort(f"{name} {part}: 재배열 후에도 위반 {left}건")
            replacements[part] = data

    print(f"{name}")
    print(f"  위반 {total}  ({' / '.join(detail)})")
    if unlisted:
        items = ", ".join(f"{k} {v}" for k, v in sorted(unlisted.items()))
        print(f"  목록 밖 요소 (--fix 시 맨 뒤로): {items}")

    if fix:
        if replacements:
            try:
                write_zip(path, replacements)
            except OSError as e:
                raise Abort(f"{name}: 저장 실패 — {e} (Word 에서 열려 있는지 확인)")
            print(f"  --fix: rPr·pPr {reordered}곳 재배열 · 문단 수/텍스트 동일 확인 · 저장함")
        else:
            print("  --fix: 바꿀 것 없음")
    return total


def main():
    ap = argparse.ArgumentParser(description="정본 docx rPr/pPr 자식 순서 검사")
    ap.add_argument("docs", nargs="*", default=DOCS, choices=DOCS, metavar="문서",
                    help="A~F 중 일부 (기본: 전부)")
    ap.add_argument("--fix", action="store_true", help="ECMA-376 순서로 재배열해 덮어쓴다")
    args = ap.parse_args()

    paths = [find_doc(d) for d in args.docs]
    grand = 0
    try:
        for path in paths:
            grand += process(path, args.fix)
    except Abort as e:
        print(f"[중단] {e} — 저장하지 않았다.", file=sys.stderr)
        return 2

    print(f"\n합계 위반 {grand}건 ({len(paths)}개 파일)")
    if args.fix:
        return 0
    return 1 if grand else 0


if __name__ == "__main__":
    sys.exit(main())
