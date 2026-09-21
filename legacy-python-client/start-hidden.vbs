Set s = CreateObject("WScript.Shell")
base = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
s.Run """" & base & "\venv\Scripts\pythonw.exe"" """ & base & "\client.py""", 0, False
