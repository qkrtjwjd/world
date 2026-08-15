using UnityEditor;
using UnityEngine;
using static MapCameraZone;

[CustomEditor(typeof(MapCameraZone))]
public class MapCameraZoneEditor : Editor
{
    SerializedProperty _type;

    // Track
    SerializedProperty _trackSmoothTime;
    SerializedProperty _trackLagTime;
    SerializedProperty _lookAheadAmount;
    SerializedProperty _lookAheadSmoothTime;

    // Trigger
    SerializedProperty _zoomAmount;
    SerializedProperty _zoomDuration;
    SerializedProperty _restoreOnExit;
    SerializedProperty _stopDuration;
    SerializedProperty _tiltAngle;
    SerializedProperty _tiltReturn;
    SerializedProperty _fireOnce;

    // Transition
    SerializedProperty _fadeDuration;
    SerializedProperty _targetName;
    SerializedProperty _povAngle;
    SerializedProperty _panHeight;
    SerializedProperty _panSpeed;

    static readonly (string type, string hint)[] _hints = new[]
    {
        ("TrackFollow",       "플레이어가 존 안에 있는 동안 smoothTime 적용. 퇴장 시 자동 복귀."),
        ("TrackLag",          "뒤처지는 카메라 효과. 루 혼자 남을 때 등 고독감 연출에 적합."),
        ("TrackForward",      "이동 방향으로 카메라 X 오프셋. 수풀 진입처럼 앞을 보여줄 때 사용."),
        ("TriggerZoom",       "진입 시 줌인. restoreOnExit 체크 시 퇴장 때 원래 줌으로 복귀."),
        ("TriggerStop",       "진입 시 카메라가 stopDuration(초) 동안 정지 후 자동 복귀."),
        ("TriggerTilt",       "넘어짐·충격 연출. 진입 시 기울고 tiltReturn(초) 후 자동 복귀."),
        ("TransitionFade",    "진입 시 화면 페이드 아웃. 문/포털 진입 전 배치할 것."),
        ("TransitionCut",     "즉시 컷. targetName = 씬 오브젝트명 (비워두면 현재 타깃 유지)."),
        ("TransitionPanDown", "위→아래 팬. 쉼터 진입 등 내려가는 느낌의 전환에 사용."),
        ("TransitionPov",     "1인칭 시점. targetName 오브젝트 위치로 이동 + Z축 회전."),
    };

    void OnEnable()
    {
        _type = serializedObject.FindProperty("type");

        _trackSmoothTime     = serializedObject.FindProperty("trackSmoothTime");
        _trackLagTime        = serializedObject.FindProperty("trackLagTime");
        _lookAheadAmount     = serializedObject.FindProperty("lookAheadAmount");
        _lookAheadSmoothTime = serializedObject.FindProperty("lookAheadSmoothTime");

        _zoomAmount    = serializedObject.FindProperty("zoomAmount");
        _zoomDuration  = serializedObject.FindProperty("zoomDuration");
        _restoreOnExit = serializedObject.FindProperty("restoreOnExit");
        _stopDuration  = serializedObject.FindProperty("stopDuration");
        _tiltAngle     = serializedObject.FindProperty("tiltAngle");
        _tiltReturn    = serializedObject.FindProperty("tiltReturn");
        _fireOnce      = serializedObject.FindProperty("fireOnce");

        _fadeDuration = serializedObject.FindProperty("fadeDuration");
        _targetName   = serializedObject.FindProperty("targetName");
        _povAngle     = serializedObject.FindProperty("povAngle");
        _panHeight    = serializedObject.FindProperty("panHeight");
        _panSpeed     = serializedObject.FindProperty("panSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_type, new GUIContent("Zone Type"));
        EditorGUILayout.Space(4);

        var t = (CamZoneType)_type.enumValueIndex;

        switch (t)
        {
            case CamZoneType.TrackFollow:
                EditorGUILayout.PropertyField(_trackSmoothTime, new GUIContent("Smooth Time", "카메라 따라가는 속도 (낮을수록 빠름)"));
                break;

            case CamZoneType.TrackLag:
                EditorGUILayout.PropertyField(_trackLagTime, new GUIContent("Lag Time", "카메라 지연 시간 (높을수록 느리게 따라옴)"));
                break;

            case CamZoneType.TrackForward:
                EditorGUILayout.PropertyField(_lookAheadAmount,     new GUIContent("Look Ahead Amount",      "이동 방향으로 오프셋 거리"));
                EditorGUILayout.PropertyField(_lookAheadSmoothTime, new GUIContent("Look Ahead Smooth Time", "오프셋 전환 속도"));
                break;

            case CamZoneType.TriggerZoom:
                EditorGUILayout.PropertyField(_zoomAmount,    new GUIContent("Zoom Amount",     "줄어드는 OrthoSize 크기"));
                EditorGUILayout.PropertyField(_zoomDuration,  new GUIContent("Zoom Duration",   "줌인 소요 시간 (초)"));
                EditorGUILayout.PropertyField(_restoreOnExit, new GUIContent("Restore On Exit", "퇴장 시 원래 줌으로 복귀"));
                EditorGUILayout.PropertyField(_fireOnce,      new GUIContent("Fire Once",       "한 번 발동 후 재진입 무시"));
                break;

            case CamZoneType.TriggerStop:
                EditorGUILayout.PropertyField(_stopDuration, new GUIContent("Stop Duration", "카메라 정지 시간 (초)"));
                EditorGUILayout.PropertyField(_fireOnce,     new GUIContent("Fire Once",     "한 번 발동 후 재진입 무시"));
                break;

            case CamZoneType.TriggerTilt:
                EditorGUILayout.PropertyField(_tiltAngle,  new GUIContent("Tilt Angle",  "기울기 각도 (°)"));
                EditorGUILayout.PropertyField(_tiltReturn, new GUIContent("Return Time", "복귀까지 대기 시간 (초)"));
                EditorGUILayout.PropertyField(_fireOnce,   new GUIContent("Fire Once",   "한 번 발동 후 재진입 무시"));
                break;

            case CamZoneType.TransitionFade:
                EditorGUILayout.PropertyField(_fadeDuration, new GUIContent("Fade Duration", "페이드 아웃 소요 시간 (초)"));
                EditorGUILayout.PropertyField(_fireOnce,     new GUIContent("Fire Once",     "한 번 발동 후 재진입 무시"));
                break;

            case CamZoneType.TransitionCut:
                EditorGUILayout.PropertyField(_targetName, new GUIContent("Target Name", "씬의 오브젝트명. 비워두면 현재 타깃 유지."));
                EditorGUILayout.PropertyField(_fireOnce,   new GUIContent("Fire Once",   "한 번 발동 후 재진입 무시"));
                break;

            case CamZoneType.TransitionPanDown:
                EditorGUILayout.PropertyField(_panHeight, new GUIContent("Pan Height", "팬 이동 높이 (양수 = 위→아래)"));
                EditorGUILayout.PropertyField(_panSpeed,  new GUIContent("Pan Speed",  "팬 속도"));
                EditorGUILayout.PropertyField(_fireOnce,  new GUIContent("Fire Once",  "한 번 발동 후 재진입 무시"));
                break;

            case CamZoneType.TransitionPov:
                EditorGUILayout.PropertyField(_targetName, new GUIContent("Target Name", "POV 기준 오브젝트명"));
                EditorGUILayout.PropertyField(_povAngle,   new GUIContent("POV Angle",   "Z축 회전 각도 (°)"));
                EditorGUILayout.PropertyField(_fireOnce,   new GUIContent("Fire Once",   "한 번 발동 후 재진입 무시"));
                break;
        }

        // 타입별 안내 메시지
        string hint = GetHint(t.ToString());
        if (!string.IsNullOrEmpty(hint))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(hint, MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();
    }

    static string GetHint(string typeName)
    {
        foreach (var (type, hint) in _hints)
            if (type == typeName) return hint;
        return "";
    }
}
