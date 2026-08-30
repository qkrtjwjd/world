using System;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

/// <summary>
/// Assets/Art/ 아래로 들어오는 텍스처에 도트 규격 임포트 프리셋을 자동 적용한다 (CLAUDE.md §11).
///
/// <para>Preset Manager 를 쓰지 않는 이유 — 그쪽 필터는 폴더가 아니라 <b>에셋 이름</b> 기준이라
/// 폴더 단위 적용이 불가능하다. 프로젝트 전체에 걸면 기존 Assets/Images/ 의 PPU 100 자산이
/// 재임포트될 때 함께 바뀐다.</para>
///
/// <para>값은 여기 적지 않는다. 단일 출처는 .preset 파일이다.</para>
/// </summary>
class PixelArtImportPostprocessor : AssetPostprocessor
{
    const string ArtRoot = "Assets/Art/";
    const string IconDir = "Assets/Art/Icons/";
    // 맵에 놓인 물건은 피벗이 Center 다. 씬의 기존 오브젝트가 Center 피벗(Square)을 전제로
    // 배치돼 있어서, 바닥 피벗을 주면 전부 위로 뜬다. 값 자체는 아이콘 프리셋과 같다.
    const string ObjectDir = "Assets/Art/Objects/";
    // 배경 타일도 Center 피벗이다. Grid 셀 1×1 의 한가운데에 놓여야 이어 붙였을 때 틈이 없다.
    const string TileDir   = "Assets/Art/Tiles/";

    const string CharacterPreset = "Assets/Settings/SpritePreset_Character.preset";
    const string IconPreset      = "Assets/Settings/SpritePreset_Icon.preset";

    void OnPreprocessTexture()
    {
        // 경로 검사가 맨 앞에 온다. 기존 Assets/Images/(PPU 100)를 지키는 유일한 장치다.
        if (!assetPath.StartsWith(ArtRoot, StringComparison.OrdinalIgnoreCase)) return;

        // 피벗이 Center 인 것 = 아이콘과 맵 오브젝트. 나머지(캐릭터·토끼)는 바닥 피벗이다.
        bool centerPivot = assetPath.StartsWith(IconDir,   StringComparison.OrdinalIgnoreCase)
                        || assetPath.StartsWith(ObjectDir, StringComparison.OrdinalIgnoreCase)
                        || assetPath.StartsWith(TileDir,   StringComparison.OrdinalIgnoreCase);
        string presetPath = centerPivot ? IconPreset : CharacterPreset;

        var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
        if (preset == null)
        {
            Debug.LogWarning($"[PixelArt] 프리셋을 찾을 수 없어 기본 설정으로 임포트됩니다: " +
                             $"{presetPath} (대상: {assetPath})");
            return;
        }

        if (!preset.ApplyTo(assetImporter))
        {
            Debug.LogWarning($"[PixelArt] 프리셋 적용에 실패했습니다: {presetPath} → {assetPath}");
            return;
        }

        // 캐릭터 프리셋의 피벗 y 는 32×48 기준 2/48 로 굳어 있다. 캔버스가 다른 자산
        // (그림자 토끼 16×16 등)에도 규격서의 「발끝 아래 2px」이 유지되도록,
        // 실제 세로 픽셀을 읽어 피벗을 다시 계산한다. 32×48 이면 값이 그대로다(2/48).
        // 아이콘은 피벗이 Center 라 대상이 아니다.
        if (!centerPivot) ApplyFootPivot();
    }

    /// <summary>발끝 아래 여백(px). 규격서 2장.</summary>
    const float FootMarginPx = 2f;

    void ApplyFootPivot()
    {
        var ti = assetImporter as TextureImporter;
        if (ti == null) return;

        int h = ReadPngHeight(assetPath);
        if (h <= 0) return;                       // PNG 가 아니거나 못 읽으면 프리셋 값을 그대로 둔다

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = new Vector2(0.5f, FootMarginPx / h);
        ti.SetTextureSettings(s);
    }

    /// <summary>PNG 헤더(IHDR)에서 세로 픽셀만 읽는다. 임포트 전이라 텍스처는 아직 없다.</summary>
    static int ReadPngHeight(string path)
    {
        try
        {
            using (var fs = System.IO.File.OpenRead(path))
            {
                var buf = new byte[24];
                if (fs.Read(buf, 0, 24) < 24) return -1;
                // 0..7 시그니처, 8..15 길이+IHDR, 16..19 너비, 20..23 높이 (빅엔디안)
                if (buf[0] != 0x89 || buf[1] != 'P' || buf[2] != 'N' || buf[3] != 'G') return -1;
                return (buf[20] << 24) | (buf[21] << 16) | (buf[22] << 8) | buf[23];
            }
        }
        catch
        {
            return -1;
        }
    }
}
