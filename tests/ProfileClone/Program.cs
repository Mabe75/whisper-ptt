// Prueft, dass SettingsForm.Clone jedes beschreibbare Profilfeld mitnimmt.
// Genau hier ging in 2.1.3 insert_mode/paste_keys/newline verloren.
using System.Reflection;
using VoicePTT;

var clone = typeof(SettingsForm).GetMethod("Clone", BindingFlags.NonPublic | BindingFlags.Static);
if (clone == null) { Console.WriteLine("FEHLT: SettingsForm.Clone nicht gefunden."); return 1; }

// Jedes Feld auf einen Wert setzen, der sich von der Vorgabe unterscheidet.
var quelle = new Profile();
var felder = typeof(Profile).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanWrite && p.GetSetMethod() != null).ToList();
foreach (var f in felder)
{
    object wert = f.PropertyType == typeof(string) ? "pruefwert-" + f.Name.ToLowerInvariant()
                : f.PropertyType == typeof(bool) ? !(bool)f.GetValue(quelle)
                : f.PropertyType == typeof(int) ? (int)f.GetValue(quelle) + 123
                : null;
    if (wert == null) { Console.WriteLine("UNKLAR: " + f.Name + " (" + f.PropertyType.Name + ") - Test erweitern."); return 1; }
    f.SetValue(quelle, wert);
}

var kopie = (Profile)clone.Invoke(null, new object[] { quelle });
var fehler = felder.Where(f => !Equals(f.GetValue(quelle), f.GetValue(kopie))).ToList();

Console.WriteLine("Profilfelder geprueft: " + felder.Count);
foreach (var f in fehler)
    Console.WriteLine("  VERLOREN: " + f.Name + " -> Clone liefert \"" + f.GetValue(kopie) + "\"");
Console.WriteLine(fehler.Count == 0 ? "OK: Clone kopiert alle Felder." : "FEHLER: " + fehler.Count + " Feld(er) gehen verloren.");
return fehler.Count == 0 ? 0 : 1;
