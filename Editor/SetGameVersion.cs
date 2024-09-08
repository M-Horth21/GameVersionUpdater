using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace OutputEnable.Scripts
{
  [Serializable]
  public class SetGameVersion : EditorWindow
  {
    // Variables that we want Unity to store values of.
    [SerializeField] int _newMajor;
    [SerializeField] int _newMinor;
    [SerializeField] int _newPatch;
    [SerializeField] string _patchNotes;

    Version _currentVersion;

    bool Different => Version.Parse($"{_newMajor}.{_newMinor}.{_newPatch}") > _currentVersion;
    bool ReadyToApply => Different && !string.IsNullOrEmpty(_patchNotes);

    [MenuItem("Tools/Update game version")]
    static void OpenWindow()
    {
      var window = GetWindow<SetGameVersion>(true, "Update game version");
      window.minSize = new Vector2(400, 600);
    }

    void OnEnable()
    {
      ResetVersion();

      // Subscribe to the undo/redo performed event to repaint the window.
      Undo.undoRedoPerformed += HandleUndoRedoPerformed;
    }

    void OnDisable()
    {
      Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
    }

    void HandleUndoRedoPerformed()
    {
      Repaint();
    }

    void OnGUI()
    {
      EditorGUILayout.LabelField($"Current version - {PlayerSettings.bundleVersion}");

      // Start a horizontal group for our buttons.
      EditorGUILayout.BeginHorizontal();

      if (GUILayout.Button("Major"))
      {
        // Major version increment, reset minor and patch after recording state for undo.
        Undo.RecordObject(this, "Increment major version");
        _newMajor++;
        _newMinor = 0;
        _newPatch = 0;
      }

      if (GUILayout.Button("Minor"))
      {
        // Minor version increment, reset patch after recording state for undo.
        Undo.RecordObject(this, "Increment minor version");
        _newMinor++;
        _newPatch = 0;
      }

      if (GUILayout.Button("Patch"))
      {
        // Patch version increment, record state for undo.
        Undo.RecordObject(this, "Increment patch version");
        _newPatch++;
      }

      // Only enable the Revert button if versions are different.
      GUI.enabled = Different;
      if (GUILayout.Button("Revert"))
      {
        // Grab the version again and overwrite any increments.
        ResetVersion();
      }
      GUI.enabled = true;

      EditorGUILayout.EndHorizontal();

      if (!Different) return;

      // When any version changes have been made, create a text area for notes and an Apply button.
      EditorGUILayout.LabelField($"Patch notes for {_newMajor}.{_newMinor}.{_newPatch}");

      // Start watching for changes in the text area.
      EditorGUI.BeginChangeCheck();
      string temp = EditorGUILayout.TextArea(_patchNotes, GUILayout.ExpandHeight(true));

      if (EditorGUI.EndChangeCheck())
      {
        // If changes detected in text, record for undo and save to notes string.
        Undo.RecordObject(this, "Change patch note text");
        _patchNotes = temp;
      }

      // Watch for keyboard shortcut if apply is valid.
      if (ReadyToApply) HandleKeyboard();

      // Enable the Apply button if valid.
      GUI.enabled = ReadyToApply;
      if (GUILayout.Button("Apply (ctrl+enter)"))
      {
        _ = ApplyBuildVersion();
      }
      GUI.enabled = true;
    }

    /// <summary>
    /// Turn the current PlayerSettings version into a Version object, then match current revisions to that version.
    /// </summary>
    void ResetVersion()
    {
      _currentVersion = Version.Parse(PlayerSettings.bundleVersion);

      // Match new revisions to current.
      _newMajor = _currentVersion.Major;
      _newMinor = _currentVersion.Minor;
      _newPatch = _currentVersion.Build;
    }

    /// <summary>
    /// Apply the version change to PlayerSettings, then write a text file with the patch notes.
    /// </summary>
    /// <returns></returns>
    async Task ApplyBuildVersion()
    {
      if (!ReadyToApply) return;

      // Update the version string in PlayerSettings.
      PlayerSettings.bundleVersion = $"{_newMajor}.{_newMinor}.{_newPatch}";

      // Create a patch notes folder in case it doesn't already exist.
      string saveFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PatchNotes");
      Directory.CreateDirectory(saveFolder);

      // Prepare a file name with the version string.
      string fileName = Path.Combine(saveFolder, $"{PlayerSettings.productName} - v{PlayerSettings.bundleVersion} patch notes.txt");

      // Save patch notes to a file.
      using (StreamWriter sw = File.CreateText(fileName))
      {
        await sw.WriteAsync(_patchNotes);
      }

      // Clean up after applying.
      ResetVersion();
      _patchNotes = "";
      EditorUtility.DisplayDialog("Writing patch notes", $"Saved {fileName}", "OK");
      this.Close();
    }

    void HandleKeyboard()
    {
      // Watch for control+enter press.
      var current = Event.current;
      if (current.type != EventType.KeyDown) return;

      if (current.keyCode != KeyCode.Return) return;
      if (current.modifiers != EventModifiers.Control) return;

      _ = ApplyBuildVersion();
    }
  }
}