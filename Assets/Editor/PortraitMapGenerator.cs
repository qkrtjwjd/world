// 코드가 찾는 포트레이트 파일명을 CharacterSpriteData 에서 뽑아 문서로 남긴다.
// 감정 ID 를 늘리거나 바꾸면 다시 돌릴 것. 읽기만 하고 에셋은 건드리지 않는다.
//
// 로드 순서 (YarnCommandBridge:625-648)
//   ① 게이지 70 이상이면 Resources/Sprites/{화자}_{감정}_real 을 먼저 본다
//   ② Resources/Sprites/{화자}_{감정}
//   ③ CharacterSpriteData 의 sprite 슬롯
//   전부 없으면 초상화를 숨기고 대사만 진행한다 (크래시 없음)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PortraitMapGenerator
{
    const string DATA = "Assets/Date/Dialogue/CharacterSpriteData.asset";

    [MenuItem("Tools/문서/포트레이트 파일명 표 갱신")]
    public static void Run()
    {
        string outDir = System.Environment.GetEnvironmentVariable("PM_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetTempPath();
        var md = new List<string>();
        var log = new List<string>();

        var data = AssetDatabase.LoadAssetAtPath<CharacterSpriteData>(DATA);
        if (data == null)
        {
            log.Add("FAIL 에셋을 못 읽었다: " + DATA);
            File.WriteAllLines(Path.Combine(outDir, "portrait_map.txt"), log);
            EditorApplication.Exit(1);
            return;
        }

        var so = new SerializedObject(data);
        // 구조를 모르므로 직렬화 트리를 그대로 훑는다
        var chars = so.FindProperty("characters");
        if (chars == null)
        {
            log.Add("FAIL 'characters' 배열이 없다. 실제 필드 이름:");
            var it = so.GetIterator();
            while (it.NextVisible(true)) log.Add("   " + it.propertyPath + "  (" + it.propertyType + ")");
            File.WriteAllLines(Path.Combine(outDir, "portrait_map.txt"), log);
            EditorApplication.Exit(1);
            return;
        }

        md.Add("# 포트레이트 파일명 — 코드가 찾는 이름 그대로");
        md.Add("");
        md.Add("이 문서는 `Assets/Editor/PortraitMapGenerator.cs` 가 " +
               "`Assets/Date/Dialogue/CharacterSpriteData.asset` 에서 뽑아 만든 것이다.");
        md.Add("**감정 ID 를 늘리거나 바꾸면 이 표도 다시 뽑아야 한다.**");
        md.Add("");
        md.Add("## ⚠ 규격서와 충돌하는 곳 — 먼저 읽을 것");
        md.Add("");
        md.Add("코드가 찾는 키는 `Sprites/{화자ID}_{감정}` 인데 **화자 ID 가 한글**이다(`루` `세라` `쿠루` …).");
        md.Add("그래서 `Resources` 경로로 넣으려면 파일명이 `루_neutral.png` 처럼 한글이 된다.");
        md.Add("그런데 **규격서 7장은 「파일명은 영문 소문자에 언더바. 한글로 하면 유니티에서 깨지는 경우가 있어」**" +
               " 라고 못박고 있다. 둘이 정면으로 어긋난다.");
        md.Add("");
        md.Add("→ **아래 2번(에셋 슬롯)을 쓰면 이 충돌이 사라진다.** 슬롯에 꽂는 방식은 " +
               "파일이 어디에 어떤 이름으로 있든 상관없으므로, png 는 규격서대로 영문으로 두면 된다.");
        md.Add("");
        md.Add("## 넣는 법");
        md.Add("");
        md.Add("1. `Assets/Resources/Sprites/` 에 아래 「파일명」 그대로 png 를 넣는다. " +
               "코드가 `Resources.Load` 로 먼저 찾으므로 에셋을 건드릴 필요가 없다. " +
               "**단 파일명이 한글이 된다** — 위 충돌을 보라.");
        md.Add("2. **(권장)** `Assets/Date/Dialogue/CharacterSpriteData.asset` 의 `sprite` 슬롯에 직접 꽂는다. " +
               "png 파일명은 영문으로 자유롭게 둘 수 있다.");
        md.Add("");
        md.Add("`_real` 은 **심리 게이지 70 이상**일 때 먼저 찾는 현실 버전이다. " +
               "없으면 기본 버전이 그대로 쓰인다 — 규격서 6장이 「캐릭터 두 벌은 루·세라·쿠루 셋만」" +
               "이라고 했으므로 나머지는 `_real` 을 안 만들어도 된다.");
        md.Add("");
        md.Add("아트가 하나도 없으면 초상화만 숨기고 **대사는 정상 진행**한다. 지금이 그 상태다.");
        md.Add("");

        int totalEmotions = 0, filled = 0;
        for (int i = 0; i < chars.arraySize; i++)
        {
            var c = chars.GetArrayElementAtIndex(i);
            var idProp = c.FindPropertyRelative("characterName");
            var emos = c.FindPropertyRelative("sprites");
            string cid = idProp != null ? idProp.stringValue : "(이름 필드 못 찾음)";
            if (emos == null) { log.Add("FAIL 감정 배열을 못 찾음: " + cid); continue; }

            md.Add("## " + cid + "  (" + emos.arraySize + "종)");
            md.Add("");
            md.Add("| 감정 ID | 파일명 | 현실 버전 | 지금 |");
            md.Add("|---|---|---|---|");
            for (int j = 0; j < emos.arraySize; j++)
            {
                var e = emos.GetArrayElementAtIndex(j);
                var eid = e.FindPropertyRelative("emotionId");
                var spr = e.FindPropertyRelative("sprite");
                string em = eid != null ? eid.stringValue : "?";
                bool has = spr != null && spr.objectReferenceValue != null;
                totalEmotions++;
                if (has) filled++;
                md.Add(string.Format("| `{0}` | `{1}_{0}.png` | `{1}_{0}_real.png` | {2} |",
                                     em, cid, has ? "채워짐" : "비어 있음"));
            }
            md.Add("");
            log.Add(string.Format("  {0,-10} 감정 {1}종", cid, emos.arraySize));
        }

        md.Add("---");
        md.Add("");
        md.Add(string.Format("**감정 ID 총 {0}종 · 스프라이트가 꽂힌 것 {1}개.** " +
                             "`_real` 까지 만들면 최대 {2}장이지만, 규격서 6장에 따르면 " +
                             "`_real` 이 필요한 것은 루·세라·쿠루뿐이다.",
                             totalEmotions, filled, totalEmotions * 2));

        Directory.CreateDirectory("Assets/Docs");
        File.WriteAllLines("Assets/Docs/포트레이트_파일명.md", md);

        log.Add("");
        log.Add(string.Format("감정 ID 총 {0}종 · 스프라이트 {1}개", totalEmotions, filled));
        log.Add("문서: Assets/Docs/포트레이트_파일명.md");
        File.WriteAllLines(Path.Combine(outDir, "portrait_map.txt"), log);
        AssetDatabase.Refresh();
        Debug.Log("[PortraitMapGenerator] done");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
