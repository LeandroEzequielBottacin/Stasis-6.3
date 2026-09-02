# Unity CLI Context

This project uses Unity version `6000.3.21f1`.

## Instructions for the AI Agent
When requested to interact with Unity, build the project, run tests, or execute Unity CLI commands, you **MUST** use the following path to the Unity executable:
`"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe"`

Always include the `-projectPath` argument pointing to the current directory when running Unity CLI commands:
`"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -projectPath "."`

**Example usage:**
`"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -quit -batchmode -projectPath "." -executeMethod MyEditorScript.PerformBuild`
