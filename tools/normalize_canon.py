#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
정본 폴더(.docx) 정규화 — 구글 문서에서 내려받은 docx 를 덮어쓴 뒤 돌린다 (CLAUDE.md §1-2).

    python tools/normalize_canon.py [--check] [--dir 폴더] [--backup 폴더]

    (옵션 없음)  D:\\낙원\\file 의 모든 .docx 를 정규화하고, 바뀐 파일만 백업한 뒤 덮어쓴다
    --check      검사만 한다. 아무것도 쓰지 않는다
    --dir        대상 폴더 (기본 CANON_DIR)
    --backup     백업 폴더 (기본 D:\\낙원\\backup_정규화_<YYYYMMDD_HHMMSS>)

파일마다 하는 일
  1. word/*.xml 의 모든 w:rPr · w:pPr 자식을 ECMA-376 스키마 순서로 재배열한다.
     (b → bCs, i → iCs. 목록에 없는 요소는 원래 순서를 유지한 채 맨 뒤로 가고 보고된다.)
  2. 재배열 후 문자열 <w:bCs/><w:b/> · <w:iCs/><w:i/> 개수와,
     속성이 붙은 형태(<w:b w:val="1"/> 등)까지 포함한 구조적 역순 개수가 0 인지 확인한다.
  3. styles.xml 의 단락 스타일마다 w:rPr/w:rFonts 에 ascii·hAnsi·eastAsia·cs 글꼴이 직접 적혀 있는지,
     w:lang 의 eastAsia 가 ko-KR 인지 확인하고, 없으면 넣는다.
       - 글꼴은 「맑은 고딕」. 구글 문서가 쓰는 영문명 「Malgun Gothic」은 같은 글꼴로 인정한다.
       - *Theme 속성은 직접 지정보다 우선하므로 지운다.
       - 다른 글꼴이 이미 직접 적혀 있으면 바꾸지 않고 보고만 한다.
       - 동아시아 언어가 ko-KR 이 아니면(구글 en · Word en-US) ko-KR 로 바꾼다.
     docDefaults 의 rPrDefault 에도 같은 규칙을 적용한다.
  4. 정규화 전후로 파트별 문단 텍스트와 run 단위 (텍스트 · 볼드 · 이탤릭) 가 같은지 확인한다.
     하나라도 다르면 그 파일은 저장하지 않는다.

종료 코드  0 모두 통과 · 1 확인 항목 실패(저장 안 한 파일 있음) · 2 폴더/파일 오류
"""

import argparse
import datetime
import glob
import io
import os
import shutil
import sys
import tempfile
import zipfile

from lxml import etree

for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except Exception:
        pass

CANON_DIR = r"D:\낙원\file"
BACKUP_ROOT = os.path.dirname(CANON_DIR)

FONT = "맑은 고딕"
FONT_ALIASES = {"맑은 고딕", "Malgun Gothic"}
FONT_ATTRS = ("ascii", "hAnsi", "eastAsia", "cs")
EAST_ASIA_LANG = "ko-KR"

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"

# ECMA-376 Part 1 · CT_RPr / CT_ParaRPr (ins·del·moveFrom·moveTo 는 문단 기호 rPr 에만 온다)
RPR_ORDER = ["ins", "del", "moveFrom", "moveTo",
             "rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps", "strike", "dstrike",
             "outline", "shadow", "emboss", "imprint", "noProof", "snapToGrid", "vanish", "webHidden",
             "color", "spacing", "w", "kern", "position", "sz", "szCs", "highlight", "u", "effect",
             "bdr", "shd", "fitText", "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout",
             "specVanish", "oMath", "rPrChange"]
# CT_PPr
PPR_ORDER = ["pStyle", "keepNext", "keepLines", "pageBreakBefore", "framePr", "widowControl",
             "numPr", "suppressLineNumbers", "pBdr", "shd", "tabs", "suppressAutoHyphens", "kinsoku",
             "wordWrap", "overflowPunct", "topLinePunct", "autoSpaceDE", "autoSpaceDN", "bidi",
             "adjustRightInd", "snapToGrid", "spacing", "ind", "contextualSpacing", "mirrorIndents",
             "suppressOverlap", "jc", "textDirection", "textAlignment", "textboxTightWrap",
             "outlineLvl", "divId", "cnfStyle", "rPr", "sectPr", "pPrChange"]
ORDERS = {"rPr": RPR_ORDER, "pPr": PPR_ORDER}

# CT_Style 자식 순서 — rPr 를 새로 만들 때 끼울 자리를 정한다.
STYLE_ORDER = ["name", "aliases", "basedOn", "next", "link", "autoRedefine", "hidden", "uiPriority",
               "semiHidden", "unhideWhenUsed", "qFormat", "locked", "personal", "personalCompose",
               "personalReply", "rsid", "pPr", "rPr", "tblPr", "trPr", "tcPr", "tblStylePr"]

OFF = ("0", "false", "off")


def w(tag):
    return f"{{{W}}}{tag}"


def local_w(el):
    if not isinstance(el.tag, str):
        return None
    q = etree.QName(el)
    return q.localname if q.namespace == W else None


def toggle_on(rpr, tag):
    if rpr is None:
        return False
    el = rpr.find(w(tag))
    if el is None:
        return False
    return el.get(w("val")) not in OFF


# ---------------------------------------------------------------------------
# 1 · 2  순서

def reorder(root):
    """rPr·pPr 자식 재배열. (바뀐 부모 수, 목록 밖 요소 집계)"""
    changed, unlisted = 0, {}
    for parent, order in ORDERS.items():
        rank = {n: i for i, n in enumerate(order)}
        tail = len(order)
        for el in root.iter(w(parent)):
            kids = [ch for ch in el]
            for ch in kids:
                if isinstance(ch.tag, str) and local_w(ch) not in rank:
                    key = f"{parent}/{etree.QName(ch).localname}"
                    unlisted[key] = unlisted.get(key, 0) + 1
            ordered = sorted(kids, key=lambda ch: rank.get(local_w(ch), tail))
            if ordered != kids:
                for ch in kids:
                    el.remove(ch)
                el.extend(ordered)
                changed += 1
    return changed, unlisted


def structural_inversions(root):
    """속성 유무와 무관하게 bCs 가 b 보다, iCs 가 i 보다 앞에 있는 rPr 수."""
    bad = 0
    for rpr in root.iter(w("rPr")):
        names = [local_w(ch) for ch in rpr]
        for a, b in (("b", "bCs"), ("i", "iCs")):
            if a in names and b in names and names.index(b) < names.index(a):
                bad += 1
    return bad


def literal_inversions(data):
    return data.count(b"<w:bCs/><w:b/>"), data.count(b"<w:iCs/><w:i/>")


# ---------------------------------------------------------------------------
# 3  스타일 글꼴 · 언어

def ensure_style_rpr(style):
    rpr = style.find(w("rPr"))
    if rpr is not None:
        return rpr, False
    rpr = etree.Element(w("rPr"))
    rank = {n: i for i, n in enumerate(STYLE_ORDER)}
    me = rank["rPr"]
    pos = 0
    for i, ch in enumerate(style):
        if rank.get(local_w(ch), -1) < me and local_w(ch) in rank:
            pos = i + 1
    style.insert(pos, rpr)
    return rpr, True


def fix_fonts_lang(rpr, label, notes):
    """rPr 하나의 rFonts · lang 을 확인하고 필요하면 넣는다. 고친 항목 수."""
    fixed = 0
    rf = rpr.find(w("rFonts"))
    if rf is None:
        rf = etree.SubElement(rpr, w("rFonts"))     # 위치는 뒤의 reorder 가 맞춘다
    themes = [a for a in rf.attrib if etree.QName(a).localname.endswith("Theme")]
    for attr in themes:
        del rf.attrib[attr]
    if themes:
        fixed += 1
        notes.append(("theme", label))
    for a in FONT_ATTRS:
        cur = rf.get(w(a))
        if cur is None:
            rf.set(w(a), FONT)
            fixed += 1
        elif cur not in FONT_ALIASES:
            notes.append(("font", f"{label} {a}={cur}"))

    lang = rpr.find(w("lang"))
    if lang is None:
        lang = etree.SubElement(rpr, w("lang"))
    cur = lang.get(w("eastAsia"))
    if cur != EAST_ASIA_LANG:
        lang.set(w("eastAsia"), EAST_ASIA_LANG)
        fixed += 1
        if cur is not None:
            notes.append(("lang", f"{label} {cur}"))
    return fixed


def summarize_notes(notes):
    """fix_fonts_lang 의 메모를 종류별 한 줄로 묶는다."""
    out = []
    for kind, head in (("theme", "테마 글꼴 지정 지움(직접 지정보다 우선하므로)"),
                       ("lang", "동아시아 언어 → ko-KR"),
                       ("font", "다른 글꼴이 직접 지정됨 — 바꾸지 않음")):
        items = [x for k, x in notes if k == kind]
        if items:
            shown = ", ".join(items[:6]) + (f" 외 {len(items) - 6}" if len(items) > 6 else "")
            out.append(f"{head}: {shown}")
    out += [x for k, x in notes if k == "misc"]
    return out


def fix_styles(root):
    """(검사한 단락 스타일 수, 고친 스타일 수, 메모)"""
    notes, n_styles, n_fixed = [], 0, 0
    d = root.find(f"{w('docDefaults')}/{w('rPrDefault')}")
    if d is not None:
        rpr = d.find(w("rPr"))
        if rpr is None:
            rpr = etree.SubElement(d, w("rPr"))
        if fix_fonts_lang(rpr, "docDefaults", notes):
            n_fixed += 1
    for st in root.iter(w("style")):
        if st.get(w("type")) != "paragraph":
            continue
        n_styles += 1
        rpr, _ = ensure_style_rpr(st)
        if fix_fonts_lang(rpr, f"스타일 {st.get(w('styleId'))}", notes):
            n_fixed += 1
    return n_styles, n_fixed, notes


# ---------------------------------------------------------------------------
# 4  텍스트 · 서식 스냅숏

def snapshot(root):
    """문단마다 (문단 텍스트, [(run 텍스트, 볼드, 이탤릭) ...])."""
    out = []
    for p in root.iter(w("p")):
        runs = []
        for r in p.iter(w("r")):
            t = "".join(x.text or "" for x in r.iter(w("t")))
            rpr = r.find(w("rPr"))
            runs.append((t, toggle_on(rpr, "b"), toggle_on(rpr, "i")))
        out.append(("".join(x[0] for x in runs), runs))
    return out


def snapshot_diff(before, after):
    if len(before) != len(after):
        return f"문단 수 {len(before)} → {len(after)}"
    text = sum(1 for a, b in zip(before, after) if a[0] != b[0])
    fmt = sum(1 for a, b in zip(before, after) if a[0] == b[0] and a[1] != b[1])
    if text or fmt:
        return f"텍스트 차이 {text}문단 · 서식(볼드/이탤릭) 차이 {fmt}문단"
    return None


# ---------------------------------------------------------------------------

def serialize(tree):
    return etree.tostring(tree, xml_declaration=True, encoding="UTF-8",
                          standalone=tree.docinfo.standalone)


def write_zip(path, replacements):
    """항목 순서는 원본대로, ZipInfo 는 새로 만들어 쓴다(원본 infolist 재사용 시 Overlapped entries)."""
    fd, tmp = tempfile.mkstemp(suffix=".docx", dir=os.path.dirname(path))
    os.close(fd)
    try:
        with zipfile.ZipFile(path) as src, zipfile.ZipFile(tmp, "w") as dst:
            for info in src.infolist():
                data = replacements.get(info.filename, None)
                if data is None:
                    data = src.read(info.filename)
                zi = zipfile.ZipInfo(info.filename, date_time=info.date_time)
                zi.compress_type = zipfile.ZIP_DEFLATED
                zi.external_attr = info.external_attr
                dst.writestr(zi, data)
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.remove(tmp)
        raise


def process(path):
    """파일 하나. (결과 dict) — 저장은 하지 않는다."""
    res = {"name": os.path.basename(path), "ok": True, "errors": [], "notes": [],
           "reordered": 0, "unlisted": {}, "styles": (0, 0), "lit": (0, 0), "struct": 0,
           "replacements": {}}
    with zipfile.ZipFile(path) as z:
        parts = [n for n in z.namelist()
                 if n.startswith("word/") and n.endswith(".xml") and "/_rels/" not in n]
        raw = {n: z.read(n) for n in parts}

    lit_b = lit_i = struct = 0
    for part in parts:
        data = raw[part]
        if b"<w:rPr" not in data and b"<w:pPr" not in data and part != "word/styles.xml":
            continue
        tree = etree.parse(io.BytesIO(data))
        root = tree.getroot()
        before = snapshot(root)

        if part == "word/styles.xml":
            n, f, notes = fix_styles(root)
            res["styles"] = (n, f)
            res["notes"] += notes
        n, unlisted = reorder(root)
        res["reordered"] += n
        for k, v in unlisted.items():
            res["unlisted"][k] = res["unlisted"].get(k, 0) + v

        new = serialize(tree)
        after_root = etree.fromstring(new)
        d = snapshot_diff(before, snapshot(after_root))
        if d:
            res["ok"] = False
            res["errors"].append(f"{part}: {d}")
        lb, li = literal_inversions(new)
        lit_b, lit_i = lit_b + lb, lit_i + li
        struct += structural_inversions(after_root)
        if etree.tostring(etree.fromstring(data)) != etree.tostring(after_root):
            res["replacements"][part] = new

    res["lit"], res["struct"] = (lit_b, lit_i), struct
    if lit_b or lit_i or struct:
        res["ok"] = False
        res["errors"].append(f"정규화 후 역순 남음: <w:bCs/><w:b/> {lit_b} · <w:iCs/><w:i/> {lit_i} · 구조 {struct}")
    if not res["styles"][0]:
        res["notes"].append(("misc", "단락 스타일 없음(styles.xml 확인)"))
    return res


def main():
    ap = argparse.ArgumentParser(description="정본 docx 정규화")
    ap.add_argument("--check", action="store_true", help="검사만 · 쓰지 않음")
    ap.add_argument("--dir", default=CANON_DIR)
    ap.add_argument("--backup", default=None)
    args = ap.parse_args()

    files = sorted(f for f in glob.glob(os.path.join(args.dir, "*.docx"))
                   if not os.path.basename(f).startswith("~$"))
    if not files:
        print(f"[중단] {args.dir} 에 .docx 가 없다.", file=sys.stderr)
        return 2

    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    backup = args.backup or os.path.join(BACKUP_ROOT, f"backup_정규화_{stamp}")
    failed = 0
    for path in files:
        try:
            r = process(path)
        except (zipfile.BadZipFile, KeyError, etree.XMLSyntaxError) as e:
            print(f"✗ {os.path.basename(path)}\n  읽기 실패 — {e}")
            failed += 1
            continue

        n_st, n_fx = r["styles"]
        changed = bool(r["replacements"])
        mark = "✓" if r["ok"] else "✗"
        print(f"{mark} {r['name']}")
        print(f"  순서 재배열 {r['reordered']}곳 · 역순 bCs/b {r['lit'][0]} iCs/i {r['lit'][1]} (구조 {r['struct']})"
              f" · 단락 스타일 {n_st}개(+기본값) 중 글꼴/언어 보충 {n_fx}곳 · 텍스트·서식 차이 "
              f"{'0' if not [e for e in r['errors'] if '차이' in e or '문단 수' in e] else '있음'}")
        if r["unlisted"]:
            print("  목록 밖 요소(맨 뒤로): " + ", ".join(f"{k} {v}" for k, v in sorted(r["unlisted"].items())))
        for msg in summarize_notes(r["notes"]):
            print(f"  · {msg}")
        for msg in r["errors"]:
            print(f"  ✗ {msg}")

        if not r["ok"]:
            failed += 1
            print("  → 저장 안 함")
        elif not changed:
            print("  → 바꿀 것 없음")
        elif args.check:
            print("  → 바꿀 것 있음 (--check 라 저장 안 함)")
        else:
            try:
                os.makedirs(backup, exist_ok=True)
                shutil.copy2(path, os.path.join(backup, r["name"]))
                write_zip(path, r["replacements"])
                print(f"  → 저장함 (원본 백업: {backup})")
            except OSError as e:
                failed += 1
                print(f"  ✗ 저장 실패 — {e} (Word 에서 열려 있는지 확인)")

    print(f"\n{len(files)}개 파일 · 실패 {failed}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
