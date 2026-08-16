#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
정본 원본(.docx) 열람 도구 — 읽기 전용.

정본은 저장소 밖 Desktop 에 있고 .docx 라 Read 도구로 열리지 않는다.
이 스크립트로 목차를 뽑고 필요한 절만 잘라 읽는다. (CLAUDE.md §1-1)

    python tools/read_canon.py <문서> [옵션]

    <문서>  A B C D E F | 데모 탈출 | 파일명 일부
    옵션    (없음)                 --toc 과 동일
            --toc                  제목 목록
            --sec <키>             그 절만. 번호(2-1) · 씬(S#12) · 제목 일부 · @문단번호
            --grep <정규식> [-C n] 일치 문단 + 앞뒤 n 문단 (기본 2)
            --range <a-b>          문단 번호 구간
            --all                  전문

출력은 `[문단번호] 텍스트`. 볼드 문단은 **…**, 전체 이탤릭 문단은 _…_ 로 표시한다
(§6 의 판정 근거가 서식이므로 텍스트만 뽑으면 정보가 사라진다).

docx 파싱은 새로 짜지 않고 scenario_parser.read_docx() 를 재사용한다.
"""

import argparse
import glob
import os
import re
import sys

# 콘솔이 cp949 라 한글·em dash 가 깨지거나 UnicodeEncodeError 로 죽는다. stderr 도 함께 건다.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except Exception:
        pass

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 정본 원본 위치 — 저장소 밖이다. 경로를 아는 곳은 여기 한 곳으로 둔다.
CANON_DIR = r"C:\Users\ThinkPlant\Desktop\낙원\files (1)"

# docx 파싱은 기존 파서를 재사용한다 (표준 zipfile + 정규식, 외부 의존성 없음).
sys.path.insert(0, os.path.join(
    REPO_ROOT, ".claude", "skills", "muchaesaek-scenario-to-yarn", "scripts"))
from scenario_parser import read_docx  # noqa: E402


# ---------------------------------------------------------------------------
# 문서 찾기 — 파일명에 버전이 박혀 있으므로(E_…_v2_9, F_…_v1_12) 접두로 glob 한다.
# 버전을 하드코딩하면 개정될 때마다 깨진다.

ALIASES = {
    "A": "A_*.docx", "B": "B_*.docx", "C": "C_*.docx",
    "D": "D_*.docx", "E": "E_*.docx", "F": "F_*.docx",
    "데모": "*데모_범위*.docx",
    "탈출": "*탈출_압박*.docx",
}


def find_doc(key: str) -> str:
    pattern = ALIASES.get(key.upper(), ALIASES.get(key, "*%s*.docx" % key))
    hits = sorted(glob.glob(os.path.join(CANON_DIR, pattern)))
    if len(hits) == 1:
        return hits[0]

    if not hits:
        print("'%s' 에 맞는 문서가 없다. (패턴 %s)" % (key, pattern), file=sys.stderr)
    else:
        print("'%s' 가 %d 개에 걸린다. 하나로 좁혀서 다시 부를 것:" % (key, len(hits)),
              file=sys.stderr)
    print("\n%s 안의 문서:" % CANON_DIR, file=sys.stderr)
    for f in sorted(glob.glob(os.path.join(CANON_DIR, "*.docx"))):
        print("  " + os.path.basename(f), file=sys.stderr)
    sys.exit(2)


# ---------------------------------------------------------------------------
# 제목 판정 — 문서마다 형식이 다르다. 한 패턴으로는 안 된다.
#   번호절  : A·B·C·E·F·데모·탈출
#   【      : D 의 씬 헤더 【 S#01 】
#   D-n.    : D 의 구간 표제
#   이름(En): B 의 인물 헤더  루 (Loo) …

_NUMBERED = re.compile(r"^(\d+(?:-\d+)*)\.\s")
_SCENE    = re.compile(r"^【")
_PART     = re.compile(r"^D-\d+\.")
_PERSON   = re.compile(r"^\S+\s*\([A-Za-z][^)]*\)")


def heading_of(p):
    """제목이면 (kind, level, 번호문자열) 반환, 아니면 None."""
    if not p.first_bold:
        return None
    s = p.stripped
    if not s:
        return None
    m = _NUMBERED.match(s)
    if m:
        return ("num", m.group(1).count("-") + 1, m.group(1))
    if _SCENE.match(s):
        return ("scene", 1, "")
    if _PART.match(s):
        return ("part", 0, "")
    if _PERSON.match(s):
        return ("person", 1, "")
    return None


def headings(paras):
    out = []
    for p in paras:
        h = heading_of(p)
        if h:
            out.append((p, h))
    return out


# ---------------------------------------------------------------------------
# 출력

def fmt(p) -> str:
    s = p.text.rstrip()
    if not s.strip():
        return ""
    body = s.strip()
    if p.all_italic:
        body = "_%s_" % body
    if p.first_bold:
        body = "**%s**" % body
    return "[%d] %s" % (p.idx, body)


def dump(paras):
    for p in paras:
        line = fmt(p)
        if line:
            print(line)


# ---------------------------------------------------------------------------
# 명령별 동작

def cmd_toc(paras):
    hs = headings(paras)
    if not hs:
        print("(제목으로 인식된 문단이 없다. --grep 이나 --range 를 쓸 것)")
        return
    for p, (kind, level, num) in hs:
        indent = "  " * (level - 1) if kind == "num" else ""
        print("[%4d] %s%s" % (p.idx, indent, p.stripped))
    print("\n제목 %d개 / 전체 문단 %d개" % (len(hs), len(paras)))


def cmd_sec(paras, key: str):
    hs = headings(paras)

    # @123 — 문단 번호로 직접 지정
    if key.startswith("@"):
        want = int(key[1:])
        matches = [i for i, (p, _) in enumerate(hs) if p.idx == want]
        if not matches:
            print("문단 %d 는 제목이 아니다. --toc 로 번호를 확인할 것." % want, file=sys.stderr)
            sys.exit(2)
    elif re.match(r"^\d+(-\d+)*$", key):
        # 절 번호는 정확히 일치하는 것만. 부분 문자열로 풀면 '8' 이 '4-8' 에도 걸린다.
        matches = [i for i, (_p, (_k, _l, num)) in enumerate(hs) if num == key]
    else:
        norm = key.replace(" ", "").lower()
        matches = [i for i, (p, _h) in enumerate(hs)
                   if norm in p.stripped.replace(" ", "").lower()]

    if not matches:
        print("'%s' 에 맞는 제목이 없다. --toc 로 확인할 것." % key, file=sys.stderr)
        sys.exit(2)
    if len(matches) > 1:
        print("'%s' 가 제목 %d개에 걸린다. --sec @<문단번호> 로 하나를 고를 것:"
              % (key, len(matches)), file=sys.stderr)
        for i in matches:
            print("  @%d  %s" % (hs[i][0].idx, hs[i][0].stripped), file=sys.stderr)
        sys.exit(2)

    i = matches[0]
    start_p, (kind, level, num) = hs[i]

    # 끝 지점: 번호절은 같거나 상위 레벨 제목 직전, 나머지는 같은 종류의 다음 제목 직전
    end_idx = None
    for p2, (k2, l2, _n2) in hs[i + 1:]:
        if kind == "num":
            if k2 != "num" or l2 <= level:
                end_idx = p2.idx
                break
        else:
            if k2 == kind or k2 == "part":
                end_idx = p2.idx
                break

    sel = [p for p in paras
           if p.idx >= start_p.idx and (end_idx is None or p.idx < end_idx)]
    dump(sel)
    print("\n-- %s ~ %s 문단 --" % (start_p.idx, (end_idx - 1) if end_idx else paras[-1].idx))


def cmd_grep(paras, pattern: str, ctx: int):
    rx = re.compile(pattern)
    hit = [p.idx for p in paras if rx.search(p.text)]
    if not hit:
        print("'%s' 일치 없음." % pattern)
        return
    keep = set()
    for h in hit:
        for k in range(h - ctx, h + ctx + 1):
            keep.add(k)
    prev = None
    for p in paras:
        if p.idx not in keep:
            continue
        if prev is not None and p.idx != prev + 1:
            print("   ...")
        line = fmt(p)
        if line:
            print(("> " if p.idx in hit else "  ") + line)
        prev = p.idx
    print("\n일치 문단 %d개 (앞뒤 %d문단 포함)" % (len(hit), ctx))


def cmd_range(paras, spec: str):
    m = re.match(r"^(\d+)\s*-\s*(\d+)$", spec)
    if not m:
        print("--range 는 100-200 형식.", file=sys.stderr)
        sys.exit(2)
    a, b = int(m.group(1)), int(m.group(2))
    dump([p for p in paras if a <= p.idx <= b])


# ---------------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser(add_help=True, description="정본 docx 열람 (읽기 전용)")
    ap.add_argument("doc", help="A B C D E F | 데모 탈출 | 파일명 일부")
    ap.add_argument("--toc", action="store_true", help="제목 목록 (기본값)")
    ap.add_argument("--sec", metavar="키", help="그 절만. 번호·씬·제목 일부·@문단번호")
    ap.add_argument("--grep", metavar="정규식", help="일치 문단 + 앞뒤 문단")
    ap.add_argument("-C", type=int, default=2, metavar="n", help="--grep 앞뒤 문단 수 (기본 2)")
    ap.add_argument("--range", metavar="a-b", help="문단 번호 구간")
    ap.add_argument("--all", action="store_true", help="전문")
    args = ap.parse_args()

    path = find_doc(args.doc)
    paras = read_docx(path)
    chars = sum(len(p.text) for p in paras)
    print("== %s  (문단 %d, 글자 %d) ==\n" % (os.path.basename(path), len(paras), chars))

    if args.sec:
        cmd_sec(paras, args.sec)
    elif args.grep:
        cmd_grep(paras, args.grep, args.C)
    elif args.range:
        cmd_range(paras, args.range)
    elif args.all:
        dump(paras)
    else:
        cmd_toc(paras)
    return 0


if __name__ == "__main__":
    sys.exit(main())
