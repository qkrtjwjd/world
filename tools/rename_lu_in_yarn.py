"""
무채색 낙원 — .yarn 대사의 주인공 이름 "루" 를 플레이어 지정 이름으로 치환한다.

  루:        →  {$이름}:            (화자 이름. Yarn 이 런타임에 character 마크업으로 파싱)
  루가/루는  →  {이름조사("가")}    (조사는 받침에 따라 달라지므로 함수로)
  루의/루도  →  {$이름}의           (받침과 무관한 조사는 변수 + 원문 유지)

건드리지 않는 것:
  - 쿠루 · 미루 · 하루 · 루트 등 "루" 를 포함한 다른 낱말 (앞뒤가 한글이면 제외)
  - << >> 커맨드 — <<showSprite "루" ...>> 의 "루" 는 스프라이트 조회 키다
  - // 주석 — 작성자 메모라 원문 그대로 두는 편이 읽기 좋다

사용법:
    python tools/rename_lu_in_yarn.py --dry-run     # 바뀔 내용만 출력
    python tools/rename_lu_in_yarn.py --apply       # 실제로 파일에 씀
"""

import argparse
import glob
import os
import re
import sys

# 받침에 따라 형태가 바뀌는 조사 → 함수로 처리
VARIANT_PARTICLES = ["가", "는", "를", "와", "야", "로", "랑"]
# 받침과 무관한 조사 → 변수 뒤에 원문 그대로 이어 붙임
INVARIANT_PARTICLES = ["에게", "한테", "보다", "처럼", "에서", "까지", "부터",
                       "의", "도", "만", "에", "께"]

# 긴 것부터 매칭돼야 "에게" 가 "에" 로 잘리지 않는다
_ALL = sorted(VARIANT_PARTICLES + INVARIANT_PARTICLES, key=len, reverse=True)

# 앞이 한글이면(쿠루·미루·하루) 제외, 뒤에 처리 못 한 한글이 남아도 제외
LU_PATTERN = re.compile(
    r"(?<![가-힣])루(" + "|".join(_ALL) + r")?(?![가-힣])"
)

# 치환에서 제외할 구간 (커맨드 · 주석)
COMMAND_SPAN = re.compile(r"<<.*?>>")


def convert_text(text):
    """한 줄의 '대사 본문' 부분만 치환한다. (커맨드·주석은 호출 전에 분리해 둔다)"""

    def repl(m):
        particle = m.group(1)
        if particle is None:
            return "{$이름}"
        if particle in VARIANT_PARTICLES:
            return '{이름조사("%s")}' % particle
        return "{$이름}" + particle

    return LU_PATTERN.sub(repl, text)


def convert_line(line):
    """
    한 줄을 [주석 앞 / 주석] 으로 나누고, 주석 앞에서도 << >> 구간을 빼고 치환한다.
    반환: (변환된 줄, 처리하지 못한 채 남은 '루' 개수)
    """
    # 1) // 주석 분리 — 주석은 손대지 않는다
    comment_at = line.find("//")
    body, comment = (line[:comment_at], line[comment_at:]) if comment_at >= 0 else (line, "")

    # 2) << >> 커맨드 구간을 자리표시자로 빼둔다
    commands = []

    def stash(m):
        commands.append(m.group(0))
        return "\x00%d\x00" % (len(commands) - 1)

    masked = COMMAND_SPAN.sub(stash, body)

    # 3) 치환
    converted = convert_text(masked)

    # 4) 처리 못 한 '루' 가 남았는지 확인 (쿠루·미루·하루·루트는 정상적으로 남는다)
    leftover = count_unhandled(converted)

    # 5) 커맨드 복원
    for i, cmd in enumerate(commands):
        converted = converted.replace("\x00%d\x00" % i, cmd)

    return converted + comment, leftover


UNHANDLED = re.compile(r"(?<![가-힣])루(?![가-힣])")


def count_unhandled(text):
    """치환 후에도 홀로 남은 '루' — 예상 못 한 조사가 붙은 자리일 수 있으므로 보고한다."""
    # 이미 치환된 {$이름} · {이름조사("...")} 안의 글자는 세지 않는다
    stripped = re.sub(r"\{[^}]*\}", "", text)
    return len(UNHANDLED.findall(stripped))


def process(path, apply_changes):
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")

    changes = []
    warnings = []
    out = []

    for no, line in enumerate(lines, 1):
        new_line, leftover = convert_line(line)
        out.append(new_line)
        if new_line != line:
            changes.append((no, line, new_line))
        if leftover:
            warnings.append((no, line))

    if apply_changes and changes:
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write("\n".join(out))

    return changes, warnings


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="실제로 파일에 씁니다")
    ap.add_argument("--dry-run", action="store_true", help="바뀔 내용만 출력합니다 (기본)")
    ap.add_argument("--dir", default="Assets/Dialogue", help=".yarn 파일이 있는 폴더")
    args = ap.parse_args()

    apply_changes = args.apply and not args.dry_run

    files = sorted(glob.glob(os.path.join(args.dir, "*.yarn")))
    if not files:
        print("!! .yarn 파일을 찾지 못했습니다: %s" % args.dir)
        return 1

    total_changes = 0
    total_warnings = 0

    for path in files:
        changes, warnings = process(path, apply_changes)
        total_changes += len(changes)
        total_warnings += len(warnings)

        if not changes and not warnings:
            continue

        print("\n=== %s — %d곳 변경 ===" % (os.path.basename(path), len(changes)))
        for no, before, after in changes:
            print("  %4d - %s" % (no, before.strip()))
            print("       + %s" % after.strip())

        if warnings:
            print("  !! 처리하지 못한 '루' %d곳 (예상 못 한 조사일 수 있음):" % len(warnings))
            for no, line in warnings:
                print("     %4d   %s" % (no, line.strip()))

    print("\n----------------------------------------")
    print("총 %d곳 변경%s" % (total_changes, " (적용됨)" if apply_changes else " (미적용 — --apply 로 실행하세요)"))
    if total_warnings:
        print("총 %d곳 미처리 — 위 목록을 확인하세요" % total_warnings)
    return 0


if __name__ == "__main__":
    sys.exit(main())
