import sounddevice as sd
print("--- Eingabegeraete (Input) ---")
for i, d in enumerate(sd.query_devices()):
    if d["max_input_channels"] > 0:
        print(i, "|", d["name"])
try:
    print("\nStandard:", sd.query_devices(kind="input")["name"])
except Exception:
    pass
print("\nTrage Index (z.B. 2) oder Namensteil (z.B. \"Jabra\") als input_device in config.json ein.")
