using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System;

/// <summary>
/// 렌더 파이프라인 검색과 적용을 위한 에디터 윈도우
/// </summary>
public class PipeLineChanger : EditorWindow
{
    [MenuItem("에디터툴/편의기능/PipeLineChanger")]
    private static void OpenWindow()
    {
        GetWindow<PipeLineChanger>().Show();
    }

    // 검색된 파이프라인 목록
    private List<RenderPipelineAsset> _foundPipelines = new List<RenderPipelineAsset>();
    
    // 선택 인덱스
    private int _selectedPipelineIndex = -1;
    private RenderPipelineAsset _selectedPipeline;
    
    // 변경 이력 레코드
    [Serializable]
    private class PipelineHistory
    {
        public string FromName; // 이전 이름
        public string FromType; // 이전 유형
        public string ToName;   // 새 이름
        public string ToType;   // 새 유형
        public string Timestamp; // 변경 시각
    }
    
    // 변경 이력 목록
    private List<PipelineHistory> _pipelineHistories = new List<PipelineHistory>();
    private const string PipelineHistoryKey = "PipelineChangerHistory";
    private bool _showHistory = false;
    private Vector2 _historyScrollPosition;
    // 표시 최대 수
    private const int MaxHistoryDisplayCount = 5;
    
    private void OnEnable()
    {
        // 이력 로드
        LoadPipelineHistory();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("렌더 파이프라인 설정", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        // 현재 적용 파이프라인
        DisplayCurrentRenderPipeline();
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("렌더링 파이프라인 찾기", GUILayout.Height(30)))
        {
            FindAllRenderingPipeLine();
        }
        
        EditorGUILayout.Space(10);
        
        // 파이프라인 선택
        if (_foundPipelines.Count > 0)
        {
            EditorGUILayout.LabelField("선택된 렌더 파이프라인", EditorStyles.boldLabel);
            
            string[] pipelineNames = new string[_foundPipelines.Count];
            for (int i = 0; i < _foundPipelines.Count; i++)
            {
                pipelineNames[i] = _foundPipelines[i].name;
            }
            
            int newIndex = EditorGUILayout.Popup(_selectedPipelineIndex, pipelineNames);
            if (newIndex != _selectedPipelineIndex)
            {
                _selectedPipelineIndex = newIndex;
                _selectedPipeline = _foundPipelines[_selectedPipelineIndex];
            }
            
            EditorGUILayout.Space(20);
            
            // 적용
            GUI.enabled = _selectedPipelineIndex >= 0;
            if (GUILayout.Button("적용하기", GUILayout.Height(30)))
            {
                ApplyRenderingPipeLine();
            }
            GUI.enabled = true;
        }
        else
        {
            EditorGUILayout.HelpBox("파이프라인을 먼저 검색해주세요.", MessageType.Info);
        }
        
        // 변경 이력
        EditorGUILayout.Space(20);
        _showHistory = EditorGUILayout.Foldout(_showHistory, "렌더링 파이프라인 변경 이력", true);
        
        if (_showHistory && _pipelineHistories.Count > 0)
        {
            DisplayPipelineHistory();
        }
    }
    
    /// <summary>
    /// 현재 적용된 렌더 파이프라인 정보를 표시합니다.
    /// </summary>
    private void DisplayCurrentRenderPipeline()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.LabelField("현재 프로젝트에 적용된 렌더링 파이프라인", EditorStyles.boldLabel);
        
        RenderPipelineAsset currentPipeline = QualitySettings.renderPipeline;
        
        GUIStyle infoStyle = new GUIStyle(EditorStyles.label);
        infoStyle.wordWrap = true;
        
        if (currentPipeline != null)
        {
            EditorGUILayout.LabelField("이름: " + currentPipeline.name, infoStyle);

            string pipelineType = GetPipelineTypeName(currentPipeline);
            
            EditorGUILayout.LabelField("유형: " + pipelineType, infoStyle);
            EditorGUILayout.LabelField("경로: " + AssetDatabase.GetAssetPath(currentPipeline), infoStyle);
        }
        else
        {
            EditorGUILayout.LabelField("빌트인 렌더 파이프라인 (Built-in Render Pipeline)", infoStyle);
        }
        
        EditorGUILayout.EndVertical();
    }
    
    /// <summary>
    /// 파이프라인 에셋의 유형명을 반환합니다.
    /// </summary>
    /// <param name="pipeline">렌더 파이프라인 에셋</param>
    /// <returns>유형명</returns>
    private string GetPipelineTypeName(RenderPipelineAsset pipeline)
    {
        if (pipeline == null) return "빌트인 렌더 파이프라인";
        
        string pipelineType = "알 수 없음";
        if (pipeline.GetType().ToString().Contains("Universal"))
        {
            pipelineType = "Universal Render Pipeline (URP)";
        }
        else if (pipeline.GetType().ToString().Contains("HD"))
        {
            pipelineType = "High Definition Render Pipeline (HDRP)";
        }
        else if (pipeline.GetType().ToString().Contains("Lightweight"))
        {
            pipelineType = "Lightweight Render Pipeline (LWRP)";
        }
        
        return pipelineType;
    }
    
    /// <summary>
    /// 최근 변경 이력을 표시합니다.
    /// </summary>
    private void DisplayPipelineHistory()
    {
        GUIStyle historyHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
        historyHeaderStyle.alignment = TextAnchor.MiddleCenter;
        
        GUIStyle historyItemStyle = new GUIStyle(EditorStyles.label);
        historyItemStyle.wordWrap = true;
        
        GUIStyle arrowStyle = new GUIStyle(EditorStyles.boldLabel);
        arrowStyle.alignment = TextAnchor.MiddleCenter;
        arrowStyle.fontSize = 14;
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 스크롤
        _historyScrollPosition = EditorGUILayout.BeginScrollView(_historyScrollPosition, 
            GUILayout.Height(Mathf.Min(_pipelineHistories.Count, MaxHistoryDisplayCount) * 100));
        
        // 표시 개수 산정
        int displayCount = Mathf.Min(_pipelineHistories.Count, 20); // 실제 저장은 최대 20개까지
        
        for (int i = 0; i < displayCount; i++)
        {
            PipelineHistory history = _pipelineHistories[i];
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"변경 #{i + 1}: {history.Timestamp}", historyHeaderStyle);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 20));
            EditorGUILayout.LabelField("변경 전:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(history.FromName, historyItemStyle);
            EditorGUILayout.LabelField(history.FromType, historyItemStyle);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(40));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("→", arrowStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 20));
            EditorGUILayout.LabelField("변경 후:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(history.ToName, historyItemStyle);
            EditorGUILayout.LabelField(history.ToType, historyItemStyle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            if (i < displayCount - 1) // 마지막 항목이 아니면 간격
            {
                EditorGUILayout.Space(3);
            }
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    /// <summary>
    /// 변경 이력을 EditorPrefs에 저장합니다.
    /// </summary>
    private void SavePipelineHistory()
    {
        string json = JsonUtility.ToJson(new { histories = _pipelineHistories });
        EditorPrefs.SetString(PipelineHistoryKey, json);
    }
    
    /// <summary>
    /// 저장된 변경 이력을 로드합니다.
    /// </summary>
    private void LoadPipelineHistory()
    {
        if (EditorPrefs.HasKey(PipelineHistoryKey))
        {
            string json = EditorPrefs.GetString(PipelineHistoryKey);
            
            try
            {
                var wrapper = JsonUtility.FromJson<HistoryWrapper>(json);
                if (wrapper != null && wrapper.histories != null)
                {
                    _pipelineHistories = wrapper.histories;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("파이프라인 히스토리 로드 중 오류 발생: " + e.Message);
                _pipelineHistories = new List<PipelineHistory>();
            }
        }
    }
    
    /// <summary>
    /// Json 직렬화를 위한 래퍼.
    /// </summary>
    [Serializable]
    private class HistoryWrapper
    {
        public List<PipelineHistory> histories;
    }
    
    /// <summary>
    /// 프로젝트 내 모든 렌더 파이프라인 에셋을 검색합니다.
    /// </summary>
    public void FindAllRenderingPipeLine()
    {
        _foundPipelines.Clear();
        _selectedPipelineIndex = -1;
        string[] guids = AssetDatabase.FindAssets("t:RenderPipelineAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset != null)
            {
                _foundPipelines.Add(asset);
            }
        }
        Debug.Log("총 " + _foundPipelines.Count + "개의 렌더링 파이프라인을 찾았습니다.");
    }
    
    /// <summary>
    /// 선택된 렌더 파이프라인을 적용하고 이력을 기록합니다.
    /// </summary>
    public void ApplyRenderingPipeLine()
    {
        if (_selectedPipeline != null)
        {
            // 현재 파이프라인 캐시
            RenderPipelineAsset currentPipeline = QualitySettings.renderPipeline;
            
            // 레코드 생성
            PipelineHistory history = new PipelineHistory
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 변경 전
            if (currentPipeline != null)
            {
                history.FromName = currentPipeline.name;
                history.FromType = GetPipelineTypeName(currentPipeline);
            }
            else
            {
                history.FromName = "빌트인 렌더 파이프라인";
                history.FromType = "Built-in Render Pipeline";
            }
            
            // 변경 후
            history.ToName = _selectedPipeline.name;
            history.ToType = GetPipelineTypeName(_selectedPipeline);
            
            _pipelineHistories.Insert(0, history);
            
            // 최대 20개 유지
            if (_pipelineHistories.Count > 20)
            {
                _pipelineHistories.RemoveAt(_pipelineHistories.Count - 1);
            }
            
            SavePipelineHistory();
            
            QualitySettings.renderPipeline = _selectedPipeline;
            Debug.Log("렌더링 파이프라인이 적용되었습니다: " + _selectedPipeline.name);
            
            // UI 갱신
            Repaint();
        }
        else
        {
            Debug.LogWarning("적용할 렌더링 파이프라인이 선택되지 않았습니다.");
        }
    }
}
