"""
무채색 낙원 시나리오 파서 v3 — 화이트리스트 방식 (명세서 v8)

v2 와의 차이:
  v2 는 폴스루 블랙리스트였다. 마커·대사 패턴을 순서대로 시도하다가 아무것도 안 걸리면
  그 줄을 나레이션으로 그대로 출력했다. 그래서 ▶ 마커가 떨어진 연출 지시가 대사로
  출력됐고(확인필요.md 7-3), ※ 【 】 S#xx · 는 아예 인식조차 못 했다.

  v3 은 화이트리스트다. 통과시킬 4종만 정의하고, 나머지는 전부 차단하거나
  // UNCLASSIFIED 주석으로 남긴다. 조용히 새는 경로가 없다.

통과시키는 4종:
  ① 화자 대사   — 첫 run 볼드 + 허용 화자 8종 + 잔여가 따옴표로 시작
  ② 루 독백     — 문단 전체 이탤릭 + (루): '...'  또는  첫 run 볼드 + 루 (속으로) "..."
  ③ 쪽지 텍스트 — S#04D 안의 무서식 줄 (대화창이 아니라 전용 UI로 출력)
  ④ 나레이션    — node_map.json 의 narration_whitelist 에 등재된 것만

입력은 정본 .docx 다. 볼드·이탤릭이 화이트리스트의 판정 근거인데 그 정보는
docx 에만 있고 중간 .txt 에는 없다. word/document.xml 을 직접 읽는다.

사용법:
    python scenario_parser.py [정본.docx] [출력디렉토리] [--node-map 경로]

정본 경로를 생략하면 DEFAULT_DOCX 를 쓴다. 시나리오 정본은 항상 같은 파일이다.
"""

import io
import json
import os
import re
import sys
import zipfile
from dataclasses import dataclass, field
from typing import Optional

# ---------------------------------------------------------------------------
# 정본은 항상 이 문서다. 인자를 생략하면 이 경로를 쓴다.
DEFAULT_DOCX = r"C:\Users\ThinkPlant\Desktop\낙원\files (1)\D_무채색_낙원_시나리오_정본.docx"

# ---------------------------------------------------------------------------
# 허용 화자 — 이 8종 외에는 대사로 통과하지 않는다.
#   원고 표기 → (화자 ID, 표시명, 포트레이트 방향)
#   포트레이트 방향은 F-2-2 규약. PortraitRegistry.GetDefaultSide() 와 일치해야 한다.
#   문서상 단일 출처는 node_map.json 의 speaker_display 다 — 어긋나면 함께 고칠 것.
SPEAKERS: dict[str, tuple[str, str, Optional[str]]] = {
    "세라":       ("세라",     "세라",   "left"),
    "쿠루":       ("쿠루",     "쿠루",   "left"),
    "부엉이":     ("부엉이",   "부엉이", "left"),
    "루":         ("루",       "루",     "right"),
    "라디오(유)": ("radio_yu", "라디오", None),
    # D-2 마을 구간
    "솔":         ("솔",       "솔",     "left"),
    "미루":       ("미루",     "미루",   "left"),
    "아모":       ("아모",     "아모",   "left"),
}

# 「쿠루 · 공격」처럼 화자 뒤에 조건(행동명)이 붙는 표기.
# D S#17B 가 "표기는 「쿠루 · 행동명」이며 조건부 대사임을 화자 칸에서 함께 읽히게 한 것"
# 이라고 직접 명시한 형태다. 원고를 고치는 것이 아니라 파서가 양쪽을 받는다 —
# 루 독백 표기가 집·마을에서 서로 다른 것을 파서가 흡수하는 것과 같은 방향이다.
# 조건은 Item.condition 으로 남기고 화자는 앞부분으로 판정한다.
SPEAKER_WITH_CONDITION = re.compile(r"^(.+?)\s*[·ㆍ・]\s*(.+)$")

MONOLOGUE_SPEAKER_ID = "루독백"     # 포트레이트 없음. 대사창 이탤릭(런타임 적용)

# 루 독백의 원고 표기는 두 가지다.
#   ① (루): '...'        — 문단 전체 이탤릭     (집 구간)
#   ② 루 (속으로) "..."  — 첫 run 볼드          (마을 구간. 정본 [UI] "독백 처리. 대사창 이탤릭")
# ②는 볼드라서 화자 대사 판정으로 들어오므로 _try_speaker 안에서 가로챈다.
MONOLOGUE_BOLD_LABEL = "루(속으로)"      # 공백 제거 후 비교

# 문단 시작 기호로 차단하는 것들
BLOCK_PREFIXES = ("▶", "※", "·")

# 연출 데이터로 빼내는 태그 (텍스트 아님)
#
# [분기] 는 2026-08-26 정본 개정(D-3 S#20)에서 들어온 표기다.
# 「[분기] 인형화 0~30」「[분기] 인형화 31+」「[분기] 공통」 형태로 대사 묶음의 경계를 표시한다.
# 대사가 아니므로 출력하지 않지만, 버리면 47줄이 어느 분기 소속인지 산출물에서 사라진다.
# 그래서 차단이 아니라 연출 데이터로 뺀다 — staging.json 에 경계가 남는다.
STAGING_TAGS = ("[FILTER]", "[BGM]", "[SFX]", "[UI]", "[CAM]", "[TRIGGER]",
                "[목표]", "[상호작용]", "[힌트]", "[튜토리얼]", "[분기]")

SCENE_ID = re.compile(r"S#\d+[A-Z]?")

# 씬 ID 는 두 계열이다 — 본편 S#xx 와 배드 엔딩 BE#01-a (2026-08-22 정본에 추가).
# ⚠ 하이픈은 BE 쪽에만 연다. S# 패턴에 하이픈을 허용하면 S#19C-2 가 별도 씬으로 쪼개지고,
#   node_map 이 그것을 S#19C 에 합산해 둔 기대값(expected_totals 주석)이 깨진다.
SCENE_KEY = r"(?:S#\d+[A-Z]?|BE#\d+-[a-z])"
SCENE_HEADER_BRACKET = re.compile(r"^【\s*(" + SCENE_KEY + r")\s*】")
SCENE_HEADER_PLAIN = re.compile(r"^(" + SCENE_KEY + r")(?:\s|$)")

# 구간 표제 — "D-1.  집 구간" / "D-2.  마을 구간" / "D-3.  숲 구간".
# 번호를 하드코딩하지 않는다. 숲 원고가 들어오면서 늘어날 수 있다.
# CLAUDE.md §6 은 `^\*\*D-\d+\.` 로 적었지만 그것은 마크다운 표기다. docx 본문에는 `**` 가
# 없고 볼드는 run 속성이므로, `▶` 규칙과 같이 서식에 의존하지 않는 형태로 맞췄다.
SECTION_HEADER = re.compile(r"^D-\d+\.")

DQUOTE = "\"“”"           # " “ ”
SQUOTE = "'‘’"            # ' ‘ ’
MONOLOGUE = re.compile(
    r"^\(\s*루\s*\)\s*:\s*[" + SQUOTE + r"]?(.+?)[" + SQUOTE + r"]?\s*$"
)

# [TRIGGER] 본문 → 실제 존재하는 Yarn 커맨드로만 변환한다.
# YarnCommandBridge / CameraDirector 에 없는 커맨드는 지어내지 않는다.
# 매칭 안 되면 staging 에 unmapped 로 남기고 사람이 처리한다.
TRIGGER_RULES: list[tuple[re.Pattern, str]] = [
    (re.compile(r"현실.*환상.*토글|필터 토글"), "// [TRIGGER] 필터 토글 개방 — DaggerPickupCutscene 이 처리"),
]


# ---------------------------------------------------------------------------
# ① docx 추출

@dataclass
class Para:
    idx: int                 # 1-based 문단 번호 (원고 대조용)
    text: str                # run 을 concat 한 전체 텍스트
    first_bold: bool         # 첫 번째 비어 있지 않은 run 이 볼드인가
    first_text: str          # 그 run 의 텍스트
    all_italic: bool         # 비어 있지 않은 run 이 전부 이탤릭인가

    @property
    def stripped(self) -> str:
        return self.text.strip()


_P = re.compile(r"(?s)<w:p[ >].*?</w:p>")
_R = re.compile(r"(?s)<w:r[ >].*?</w:r>")
_T = re.compile(r"(?s)<w:t[^>]*>(.*?)</w:t>")
_RPR = re.compile(r"(?s)<w:rPr>.*?</w:rPr>")
_TAB = re.compile(r"<w:tab\s*/>")
_BR = re.compile(r"<w:br\s*/>")


def _toggle_on(rpr: str, tag: str) -> bool:
    """<w:b/> 는 켜짐, <w:b w:val="0"/> 는 꺼짐."""
    m = re.search(r"<w:%s(\s[^>]*)?/>" % tag, rpr)
    if not m:
        return False
    attrs = m.group(1) or ""
    v = re.search(r'w:val="([^"]*)"', attrs)
    return not (v and v.group(1) in ("0", "false", "off"))


def _unescape(s: str) -> str:
    return (s.replace("&lt;", "<").replace("&gt;", ">")
             .replace("&quot;", '"').replace("&apos;", "'")
             .replace("&amp;", "&"))


def read_docx(path: str) -> list[Para]:
    with zipfile.ZipFile(path) as z:
        xml = z.read("word/document.xml").decode("utf-8")

    paras: list[Para] = []
    for i, pm in enumerate(_P.finditer(xml), 1):
        pieces: list[tuple[str, bool, bool]] = []   # (text, bold, italic)
        for rm in _R.finditer(pm.group(0)):
            rv = rm.group(0)
            raw = "".join(_T.findall(rv))
            raw = _TAB.sub("\t", raw)
            raw = _BR.sub(" ", raw)
            t = _unescape(raw)
            if not t.strip():
                continue
            rpr_m = _RPR.search(rv)
            rpr = rpr_m.group(0) if rpr_m else ""
            pieces.append((t, _toggle_on(rpr, "b"), _toggle_on(rpr, "i")))

        text = "".join(p[0] for p in pieces)
        if not pieces:
            paras.append(Para(i, text, False, "", False))
            continue
        paras.append(Para(
            idx=i,
            text=text,
            first_bold=pieces[0][1],
            first_text=pieces[0][0],
            all_italic=all(p[2] for p in pieces),
        ))
    return paras


# ---------------------------------------------------------------------------
# ② 분류

KIND_DIALOGUE = "dialogue"
KIND_MONOLOGUE = "monologue"
KIND_NOTE = "note"
KIND_NARRATION = "narration"
KIND_STAGING = "staging"
KIND_BLOCKED = "blocked"
KIND_UNCLASSIFIED = "unclassified"


@dataclass
class Item:
    kind: str
    para: int
    scene: Optional[str]
    text: str = ""
    speaker_id: str = ""
    display: str = ""
    side: Optional[str] = None
    tag: str = ""
    reason: str = ""
    condition: str = ""      # 「쿠루 · 공격」의 '공격'. 조건부 대사만 채워진다


@dataclass
class SceneBucket:
    scene: str
    dialogue: list[Item] = field(default_factory=list)
    note: list[Item] = field(default_factory=list)
    narration: list[Item] = field(default_factory=list)
    staging: list[Item] = field(default_factory=list)
    unclassified: list[Item] = field(default_factory=list)


class Classifier:
    """판정 순서가 곧 로직이다. 위에서부터 처음 맞는 규칙에서 확정한다."""

    def __init__(self, narration_whitelist: list[str], note_scene: str) -> None:
        self.narration_whitelist = narration_whitelist
        self.note_scene = note_scene
        self.scene: Optional[str] = None

    def classify(self, p: Para) -> Optional[Item]:
        s = p.stripped
        if not s:
            return None

        # 1. 씬 헤더 — 【 S#xx 】 및 S#xx 줄
        m = SCENE_HEADER_BRACKET.match(s) or SCENE_HEADER_PLAIN.match(s)
        if m:
            self.scene = m.group(1)
            return Item(KIND_BLOCKED, p.idx, self.scene, s, reason="씬 헤더")
        if s.startswith("【"):
            return Item(KIND_BLOCKED, p.idx, self.scene, s, reason="씬 헤더")

        # 1b. 구간 표제 — D-1 / D-2 / D-3 …
        if SECTION_HEADER.match(s):
            return Item(KIND_BLOCKED, p.idx, self.scene, s, reason="구간 표제")

        # 2. ▶ 연출 — 볼드 여부 무관 (원고에 볼드 누락 10건)
        # 3. ※ 집필 주석
        # 4. · 에셋 목록
        for pref, why in zip(BLOCK_PREFIXES, ("연출 지시", "집필 주석", "에셋 목록")):
            if s.startswith(pref):
                return Item(KIND_BLOCKED, p.idx, self.scene, s, reason=why)

        # 5. 연출 태그 블록 → 연출 데이터로 분리 (텍스트 아님)
        for tag in STAGING_TAGS:
            if s.startswith(tag):
                body = s[len(tag):].strip()
                return Item(KIND_STAGING, p.idx, self.scene, body, tag=tag)

        # 6. 화이트리스트 ② — 루 독백 (이탤릭이므로 10번보다 반드시 위)
        if p.all_italic:
            m = MONOLOGUE.match(s)
            if m:
                return Item(KIND_MONOLOGUE, p.idx, self.scene, m.group(1).strip(),
                            speaker_id=MONOLOGUE_SPEAKER_ID, display="", side=None)

        # 7. 화이트리스트 ① — 화자 대사
        item = self._try_speaker(p, s)
        if item is not None:
            return item

        # 8. 화이트리스트 ③ — 쪽지 (S#04D 안의 무서식 줄)
        if self.scene == self.note_scene and not p.first_bold and not p.all_italic:
            return Item(KIND_NOTE, p.idx, self.scene, s)

        # 9. 화이트리스트 ④ — 나레이션 예외 (이탤릭이므로 10번보다 위)
        for allowed in self.narration_whitelist:
            if s.startswith(allowed):
                return Item(KIND_NARRATION, p.idx, self.scene, allowed)

        # 10. 이탤릭 지문 → 차단
        if p.all_italic:
            return Item(KIND_BLOCKED, p.idx, self.scene, s, reason="이탤릭 지문")

        # 11. 판정 실패 — 버리지 않고 남긴다
        return Item(KIND_UNCLASSIFIED, p.idx, self.scene, s)

    # ------------------------------------------------------------------
    @staticmethod
    def _try_speaker(p: Para, s: str) -> Optional[Item]:
        if not p.first_bold:
            return None
        name = p.first_text.strip()
        mono = re.sub(r"\s+", "", name) == MONOLOGUE_BOLD_LABEL
        condition = ""
        if not mono and name not in SPEAKERS:
            # 「쿠루 · 행동명」 — 화자 칸에 조건이 함께 적힌 표기 (D S#17B)
            m = SPEAKER_WITH_CONDITION.match(name)
            if not m or m.group(1).strip() not in SPEAKERS:
                return None
            name, condition = m.group(1).strip(), m.group(2).strip()

        rest = s[len(p.first_text.rstrip()):].lstrip()
        # 정본 42문단은 닫는 따옴표가 없고, 252문단은 여는 " 에 닫는 ” 이다.
        # 여는 따옴표만 요구하고, 닫는 쪽은 있으면 떼어낸다.
        if not rest or rest[0] not in DQUOTE:
            return None
        body = rest[1:]
        if body and body[-1] in DQUOTE:
            body = body[:-1]

        if mono:
            return Item(KIND_MONOLOGUE, p.idx, None, body.strip(),
                        speaker_id=MONOLOGUE_SPEAKER_ID, display="", side=None)

        sid, display, side = SPEAKERS[name]
        return Item(KIND_DIALOGUE, p.idx, None, body.strip(),
                    speaker_id=sid, display=display, side=side,
                    condition=condition)


# ---------------------------------------------------------------------------
# ④ 이름 치환 — 정본의 "루" 를 {$이름} / {이름조사("가")} 로

def _load_name_converter(repo_root: str):
    """tools/rename_lu_in_yarn.py 의 convert_text 를 재사용한다."""
    tools = os.path.join(repo_root, "tools")
    if tools not in sys.path:
        sys.path.insert(0, tools)
    try:
        from rename_lu_in_yarn import convert_text     # type: ignore
        return convert_text
    except Exception as e:                              # noqa: BLE001
        print("[경고] tools/rename_lu_in_yarn.py 를 불러오지 못했습니다 — "
              "이름 치환을 건너뜁니다: %s" % e, file=sys.stderr)
        return None


# ---------------------------------------------------------------------------
# ③⑤ 매핑 · 게이트 · 출력

class Converter:
    def __init__(self, node_map: dict, repo_root: str) -> None:
        self.map = node_map
        self.repo_root = repo_root
        self.convert_name = _load_name_converter(repo_root)
        self.buckets: dict[str, SceneBucket] = {}
        self.order: list[str] = []
        self.failures: list[str] = []

    # ------------------------------------------------------------------
    def run(self, docx_path: str, out_dir: str) -> int:
        paras = read_docx(docx_path)
        cls = Classifier(self.map["narration_whitelist"], self.map["note_scene"])

        for p in paras:
            item = cls.classify(p)
            if item is None:
                continue
            scene = cls.scene or "(씬 이전)"
            b = self.buckets.get(scene)
            if b is None:
                b = self.buckets[scene] = SceneBucket(scene)
                self.order.append(scene)

            if item.kind in (KIND_DIALOGUE, KIND_MONOLOGUE):
                item.scene = scene
                b.dialogue.append(item)
            elif item.kind == KIND_NOTE:
                b.note.append(item)
            elif item.kind == KIND_NARRATION:
                b.narration.append(item)
            elif item.kind == KIND_STAGING:
                b.staging.append(item)
            elif item.kind == KIND_UNCLASSIFIED:
                b.unclassified.append(item)

        os.makedirs(out_dir, exist_ok=True)
        assigned = self._assign_nodes()
        self._check_gate()
        self._emit_yarn(out_dir, assigned)
        self._emit_staging(out_dir)
        self._emit_notes(out_dir)
        self._emit_unclassified(out_dir)
        self._emit_diff(out_dir, assigned)
        return self._emit_gate(out_dir)

    # ------------------------------------------------------------------
    def _assign_nodes(self) -> dict[str, list[tuple[str, list[Item]]]]:
        """씬의 대사를 node_map 의 count 만큼 노드 순서대로 잘라 배분한다."""
        by_file: dict[str, list[tuple[str, list[Item]]]] = {}

        for spec in self.map["scenes"]:
            scene = spec["scene"]
            b = self.buckets.get(scene)
            dialogue = b.dialogue if b else []
            # 나레이션은 대사와 같은 대화창에 뜨므로 원고 순서 그대로 섞어 배분한다.
            # (쪽지는 전용 UI 라서 여기 들어가지 않는다.)
            stream = sorted(dialogue + (b.narration if b else []), key=lambda i: i.para)

            pos = 0
            used = 0
            chunks: list[list[Item]] = []
            for nd in spec["nodes"]:
                n = nd["count"]
                chunk: list[Item] = []
                taken = 0
                while pos < len(stream) and taken < n:
                    it = stream[pos]
                    pos += 1
                    chunk.append(it)
                    if it.kind != KIND_NARRATION:
                        taken += 1
                used += taken
                chunks.append(chunk)

                anchor = nd.get("anchor")
                if anchor:
                    first = next((c for c in chunk if c.kind != KIND_NARRATION), None)
                    if first is None:
                        self.failures.append(
                            "%s / %s : 대사가 배분되지 않음 (앵커 '%s')"
                            % (scene, nd["node"], anchor))
                    elif not first.text.startswith(anchor):
                        self.failures.append(
                            "%s / %s : 앵커 불일치\n      기대 '%s'\n      실제 '%s'"
                            % (scene, nd["node"], anchor, first.text[:40]))

            # 마지막 대사 뒤에 남은 나레이션은 마지막 노드에 붙인다
            if pos < len(stream) and chunks:
                chunks[-1].extend(stream[pos:])
                pos = len(stream)

            for nd, chunk in zip(spec["nodes"], chunks):
                by_file.setdefault(spec["file"], []).append((nd["node"], chunk))

            if used != len(dialogue):
                self.failures.append(
                    "%s : 노드 count 합 %d 인데 실제 대사 %d 줄 — %d 줄이 어느 노드에도 안 들어감"
                    % (scene, used, len(dialogue), len(dialogue) - used))
            if pos != len(stream):
                self.failures.append(
                    "%s : 나레이션 %d 줄이 어느 노드에도 안 들어감"
                    % (scene, len(stream) - pos))

        return by_file

    # ------------------------------------------------------------------
    def _check_gate(self) -> None:
        totals = self.map["expected_totals"]
        d = n = r = 0
        self._speaker_totals: dict[str, int] = {}

        for spec in self.map["scenes"]:
            scene = spec["scene"]
            b = self.buckets.get(scene)
            got = len(b.dialogue) if b else 0
            d += got
            if b:
                n += len(b.note)
                r += len(b.narration)
            if got != spec["expected"]:
                self.failures.append(
                    "%s : 대사 %d 줄 (기대 %d)" % (scene, got, spec["expected"]))
            self._check_by_speaker(spec, b)

        # node_map 에 없는 씬에서 대사가 나온 경우
        known = {s["scene"] for s in self.map["scenes"]}
        for scene in self.order:
            b = self.buckets[scene]
            if scene not in known and (b.dialogue or b.note or b.narration):
                self.failures.append(
                    "%s : node_map 에 없는 씬인데 통과 항목이 있음 (대사 %d / 쪽지 %d / 나레이션 %d)"
                    % (scene, len(b.dialogue), len(b.note), len(b.narration)))
                d += len(b.dialogue)
                n += len(b.note)
                r += len(b.narration)

        for label, got, want in (("대사", d, totals["dialogue"]),
                                 ("쪽지", n, totals["note"]),
                                 ("나레이션", r, totals["narration"])):
            if got != want:
                self.failures.append("합계 %s : %d (기대 %d)" % (label, got, want))

        if d + n + r != totals["all"]:
            self.failures.append(
                "총 표시 줄 : %d (기대 %d)" % (d + n + r, totals["all"]))

        self._counts = (d, n, r)

    # ------------------------------------------------------------------
    def _check_by_speaker(self, spec: dict, b: Optional[SceneBucket]) -> None:
        """게이트 2 — 화자별 배분 대조 (CLAUDE.md §6).

        씬 합계가 맞아도 화자가 밀린 경우를 잡는다. 실패 메시지는 뭉뚱그리지 않고
        화자별 기대/산출과 차이의 방향(+/-)을 찍는다. 인접 화자 간 밀림이 바로 보인다.
        루독백은 별도 화자로 계상한다 — 화자대사 카운트에 합치지 않는다.
        """
        scene = spec["scene"]

        got: dict[str, int] = {}
        for it in (b.dialogue if b else []):
            got[it.speaker_id] = got.get(it.speaker_id, 0) + 1
        for sid, c in got.items():
            self._speaker_totals[sid] = self._speaker_totals.get(sid, 0) + c

        want = spec.get("by_speaker")
        if want is None:
            # 데이터만 있고 파서가 읽지 않는 상태를 두지 않는다 — 미등재도 실패다.
            self.failures.append(
                "%s : by_speaker 미등재 — node_map.json 에 화자별 배분을 적을 것 "
                "(산출: %s)" % (scene, self._fmt_counts(got) or "0줄"))
            return

        if got == want:
            return

        rows = []
        for sid in sorted(set(want) | set(got)):
            w, g = want.get(sid, 0), got.get(sid, 0)
            if w == g:
                continue
            rows.append("%s: 기대 %d / 산출 %d  (%+d)" % (sid, w, g, g - w))

        # 리포트가 각 항목 앞에 "  " 를 붙이지만 줄바꿈 뒤에는 안 붙는다 → 그만큼 더 들여쓴다.
        head = "FAIL %s  " % scene
        pad = " " * (len(head) + 2)
        self.failures.append(head + ("\n" + pad).join(rows))

    @staticmethod
    def _fmt_counts(d: dict[str, int]) -> str:
        return "·".join("%s%d" % (k, v) for k, v in sorted(d.items()))

    # ------------------------------------------------------------------
    def _fmt_line(self, it: Item) -> str:
        text = it.text
        if self.convert_name:
            text = self.convert_name(text)
        if it.kind == KIND_NARRATION:
            # 화자 없는 줄. Yarn Spinner 가 이름창을 자동으로 숨긴다.
            return text
        speaker = it.speaker_id
        if speaker == "루" and self.convert_name:
            speaker = "{$이름}"
        return "%s: %s" % (speaker, text)

    def _emit_yarn(self, out_dir: str, by_file: dict) -> None:
        scene_file = {s["scene"]: s["file"] for s in self.map["scenes"]}
        for stem, nodes in by_file.items():
            out = [
                "// ─────────────────────────────────────────────────────────────",
                "// 무채색 낙원 — %s (화이트리스트 변환 v3 자동 생성)" % stem,
                "// 정본: D_무채색_낙원_시나리오_정본.docx",
                "// 매핑: Scenario/node_map.json",
                "//",
                "// ⚠ 이 파일은 '표시 텍스트 레이어'만 담는다.",
                "//   연출 커맨드(<<set_filter>> <<play_sfx>> <<camera_*>> 등)는 들어 있지 않다.",
                "//   연출은 %s_staging.json 에 분리돼 있다." % stem,
                "// ⚠ Assets/Dialogue 에 그대로 덮어쓰지 말 것 — 수동 보정 레이어가 날아간다.",
                "// ─────────────────────────────────────────────────────────────",
                "",
            ]
            for node, items in nodes:
                out.append("title: %s" % node)
                out.append("---")
                for it in items:
                    out.append(self._fmt_line(it))
                if not items:
                    out.append("// 이 노드에는 정본상 대사가 없다 (연출·상호작용만).")
                out.append("===")
                out.append("")

            # 판정 실패 줄은 버리지 않고 파일 끝에 남긴다 (그 파일에 속한 씬 것만)
            unc = [it for s in self.order
                   for it in self.buckets[s].unclassified
                   if scene_file.get(s, "_기타") == stem]
            if unc:
                out.append("// ── 판정 실패 (UNCLASSIFIED) — 사람이 확인할 것 ──")
                for it in unc:
                    out.append("// UNCLASSIFIED [문단%d %s] %s"
                               % (it.para, it.scene or "-", it.text))
                out.append("")

            self._write(out_dir, "%s.yarn" % stem, "\n".join(out))

    # ------------------------------------------------------------------
    def _emit_staging(self, out_dir: str) -> None:
        by_file: dict[str, dict] = {}
        scene_file = {s["scene"]: s["file"] for s in self.map["scenes"]}

        for scene in self.order:
            b = self.buckets[scene]
            if not b.staging:
                continue
            stem = scene_file.get(scene, "_기타")
            entries = by_file.setdefault(stem, {"speakers": {}, "scenes": {}})
            rows = []
            for it in b.staging:
                row = {"para": it.para, "tag": it.tag, "body": it.text}
                if it.tag == "[TRIGGER]":
                    row["command"] = None
                    for pat, cmd in TRIGGER_RULES:
                        if pat.search(it.text):
                            row["command"] = cmd
                            break
                    if row["command"] is None:
                        row["todo"] = "대응 커맨드 없음 — 사람이 결정할 것 (커맨드 지어내지 않음)"
                rows.append(row)
            entries["scenes"][scene] = rows

        for stem, data in by_file.items():
            data["speakers"] = {
                orig: {"id": sid, "display": disp, "side": side}
                for orig, (sid, disp, side) in SPEAKERS.items()
            }
            data["_about"] = (
                "[FILTER]/[BGM]/[SFX]/[UI]/[CAM]/[TRIGGER] 블록을 연출 데이터로 분리한 것. "
                "표시 텍스트가 아니다. command 가 null 인 항목은 실제 존재하는 Yarn 커맨드로 "
                "안전하게 매핑되지 않아 사람이 결정해야 한다."
            )
            self._write(out_dir, "%s_staging.json" % stem,
                        json.dumps(data, ensure_ascii=False, indent=2) + "\n")

    # ------------------------------------------------------------------
    def _emit_notes(self, out_dir: str) -> None:
        rows = []
        for scene in self.order:
            for it in self.buckets[scene].note:
                text = self.convert_name(it.text) if self.convert_name else it.text
                rows.append({"scene": scene, "para": it.para, "text": text})
        data = {
            "_about": ("읽는 물건 텍스트. 대화창이 아니라 전용 UI 로 출력한다. "
                       "현재 KitchenTriggerCutscene.noteCloseupImage 는 이미지만 켜고 끄므로 "
                       "텍스트 컴포넌트 배선이 필요하다 (Assets/Docs/유니티_수동작업.md)."),
            "notes": rows,
        }
        self._write(out_dir, "notes.json",
                    json.dumps(data, ensure_ascii=False, indent=2) + "\n")

    # ------------------------------------------------------------------
    def _emit_unclassified(self, out_dir: str) -> None:
        lines = ["=== 판정 실패 (UNCLASSIFIED) ===",
                 "버려진 것이 아니라 .yarn 끝에 주석으로도 남아 있다.",
                 "여기에 '대사'가 섞여 있으면 규칙 버그다.", ""]
        total = 0
        for scene in self.order:
            items = self.buckets[scene].unclassified
            if not items:
                continue
            lines.append("[%s]" % scene)
            for it in items:
                lines.append("  문단%-4d %s" % (it.para, it.text))
                total += 1
            lines.append("")
        lines.insert(3, "총 %d 건" % total)
        self._write(out_dir, "unclassified.txt", "\n".join(lines) + "\n")
        self._unclassified_total = total

    # ------------------------------------------------------------------
    def _emit_diff(self, out_dir: str, by_file: dict) -> None:
        """현행 Assets/Dialogue 의 '표시 텍스트'와 이번 산출물을 노드별로 대조한다.

        반영 여부는 사람이 결정한다. 이 파일은 그 판단 자료일 뿐 게이트 조건이 아니다.
        """
        import difflib

        lines = ["=== 현행 Assets/Dialogue 대비 표시 텍스트 diff ===",
                 "'-' 현행에만 있음 (반영하면 사라짐)   '+' 이번 산출물에만 있음",
                 "커맨드·주석은 비교 대상이 아니다 (이번 산출물에는 애초에 없다).",
                 ""]
        removed = added = 0

        for stem, nodes in by_file.items():
            cur_path = os.path.join(self.repo_root, "Assets", "Dialogue", "%s.yarn" % stem)
            if not os.path.exists(cur_path):
                lines.append("[%s] 현행 파일 없음 — 대조 생략\n" % stem)
                continue
            current = self._read_display_lines(cur_path)
            lines.append("### %s" % stem)
            for node, items in nodes:
                new = [self._fmt_line(it) for it in items]
                old = current.get(node, [])
                if new == old:
                    continue
                lines.append("  [%s]" % node)
                for d in difflib.unified_diff(old, new, lineterm="", n=0):
                    if d.startswith(("---", "+++", "@@")):
                        continue
                    lines.append("    " + d)
                    if d.startswith("-"):
                        removed += 1
                    elif d.startswith("+"):
                        added += 1
            for node in current:
                if node not in {n for n, _ in nodes}:
                    lines.append("  [%s] 현행에만 있는 노드 — node_map 에 없음" % node)
            lines.append("")

        lines.insert(3, "삭제 %d 줄 / 추가 %d 줄" % (removed, added))
        self._write(out_dir, "diff_vs_current.txt", "\n".join(lines) + "\n")

    @staticmethod
    def _read_display_lines(path: str) -> dict[str, list[str]]:
        out: dict[str, list[str]] = {}
        node = None
        # utf-8-sig — yarn 일부에 BOM 이 있다 (CLAUDE.md §4).
        # utf-8 로 읽으면 BOM 이 첫 줄에 붙어 "title:" 매칭이 실패하고 첫 노드를 통째로 놓친다.
        with io.open(path, encoding="utf-8-sig") as f:
            for raw in f:
                s = raw.strip()
                m = re.match(r"^title:\s*(\S+)", s)
                if m:
                    node = m.group(1)
                    out[node] = []
                    continue
                if node is None:
                    continue
                if not s or s.startswith("//") or s.startswith("<<") or s in ("---", "==="):
                    continue
                out[node].append(s)
        return out

    # ------------------------------------------------------------------
    def _emit_gate(self, out_dir: str) -> int:
        d, n, r = self._counts
        ok = not self.failures
        st = getattr(self, "_speaker_totals", {})
        mono = st.get(MONOLOGUE_SPEAKER_ID, 0)
        lines = [
            "=== 검증 게이트 ===",
            "",
            "결과: %s" % ("PASS" if ok else "FAIL"),
            "",
            "대사 %d (화자대사 %d + %s %d) / 쪽지 %d / 나레이션 %d  →  총 %d"
            % (d, d - mono, MONOLOGUE_SPEAKER_ID, mono, n, r, d + n + r),
            "UNCLASSIFIED %d 건 (실패 조건 아님 — unclassified.txt 확인)"
            % getattr(self, "_unclassified_total", 0),
            "",
            "--- 씬별 (합계 / 화자별 배분) ---",
        ]
        for spec in self.map["scenes"]:
            b = self.buckets.get(spec["scene"])
            got = len(b.dialogue) if b else 0
            per: dict[str, int] = {}
            for it in (b.dialogue if b else []):
                per[it.speaker_id] = per.get(it.speaker_id, 0) + 1
            sp_ok = per == spec.get("by_speaker", None)
            mark = "OK " if (got == spec["expected"] and sp_ok) else "!! "
            lines.append("  %s%-6s %2d / %2d   %s"
                         % (mark, spec["scene"], got, spec["expected"],
                            self._fmt_counts(per) or "-"))

        if st:
            lines += ["", "--- 화자별 총계 ---"]
            for sid in sorted(st, key=lambda k: (-st[k], k)):
                lines.append("  %-10s %2d" % (sid, st[sid]))

        if self.failures:
            lines += ["", "--- 실패 ---"]
            lines += ["  " + f for f in self.failures]

        self._write(out_dir, "gate_report.txt", "\n".join(lines) + "\n")

        print("\n".join(lines[:8]))
        if self.failures:
            print("\n실패 %d 건 — gate_report.txt 확인" % len(self.failures))
            return 1
        return 0

    # ------------------------------------------------------------------
    @staticmethod
    def _write(out_dir: str, name: str, content: str) -> None:
        path = os.path.join(out_dir, name)
        with io.open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
        print("  → %s" % path)


# ---------------------------------------------------------------------------
def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]

    # --node-map 의 값이 위치 인자로 새어 들어오지 않게 걸러낸다
    if "--node-map" in sys.argv:
        nm_val = sys.argv[sys.argv.index("--node-map") + 1]
        args = [a for a in args if a != nm_val]

    # 첫 인자가 .docx 가 아니면 정본을 생략하고 출력 디렉토리만 준 것으로 본다
    if args and args[0].lower().endswith(".docx"):
        docx_path = args.pop(0)
    else:
        docx_path = DEFAULT_DOCX
        print("[정보] 정본 경로 생략 — 기본 정본을 사용합니다:\n       %s" % DEFAULT_DOCX)
    if not os.path.exists(docx_path):
        print("[오류] 파일 없음: %s" % docx_path, file=sys.stderr)
        return 1

    repo_root = os.path.dirname(os.path.abspath(__file__))
    # 스킬 폴더에서 실행된 경우 저장소 루트를 거슬러 올라가 찾는다
    while repo_root and not os.path.isdir(os.path.join(repo_root, "Scenario")):
        parent = os.path.dirname(repo_root)
        if parent == repo_root:
            repo_root = os.getcwd()
            break
        repo_root = parent

    # 기본 출력은 output_v3. v2(블랙리스트) 산출물이 있던 Scenario/output 은
    # 2026-08-14 정리로 삭제됐다.
    out_dir = args[0] if args else os.path.join(repo_root, "Scenario", "output_v3")

    node_map_path = os.path.join(repo_root, "Scenario", "node_map.json")
    if "--node-map" in sys.argv:
        node_map_path = sys.argv[sys.argv.index("--node-map") + 1]
    if not os.path.exists(node_map_path):
        print("[오류] node_map.json 없음: %s" % node_map_path, file=sys.stderr)
        return 1

    # utf-8-sig — 윈도우 편집기로 저장하면 BOM 이 붙는다
    with io.open(node_map_path, encoding="utf-8-sig") as f:
        node_map = json.load(f)

    return Converter(node_map, repo_root).run(docx_path, out_dir)


if __name__ == "__main__":
    sys.exit(main())
