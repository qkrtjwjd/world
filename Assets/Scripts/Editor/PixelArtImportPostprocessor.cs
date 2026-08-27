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

    const string CharacterPreset = "Assets/Settings/SpritePreset_Character.preset";
    const string IconPreset      = "Assets/Settings/SpritePreset_Icon.preset";

    void OnPreprocessTexture()
    {
        // 경로 검사가 맨 앞에 온다. 기존 Assets/Images/(PPU 100)를 지키는 유일한 장치다.
        if (!assetPath.StartsWith(ArtRoot, StringComparison.OrdinalIgnoreCase)) return;

        bool isIcon = assetPath.StartsWith(IconDir, StringComparison.OrdinalIgnoreCase);
        string presetPath = isIcon ? IconPreset : CharacterPreset;

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
        }
    }
}
