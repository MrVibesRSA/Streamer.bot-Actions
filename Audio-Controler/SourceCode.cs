using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

public class CPHInline
{
	public bool VolumeUp()
	{
		CPH.TryGetArg("ActionSource", out string actionSource);
		const int maxVolumeDb = 0; // Configurable maximum
		const int volumeStep = 2;  // Configurable step size

		string requestData = "{\"inputName\":\"" + actionSource + "\"}";
		var jsonString = CPH.ObsSendRaw("GetInputVolume", requestData);
		var data = GetData.FromJson(jsonString);

		var volume = Convert.ToInt32(data.InputVolumeDb);
		if (volume < maxVolumeDb)
		{
			volume += volumeStep;

			if (volume > maxVolumeDb)
			{
				volume = maxVolumeDb;
			}

			string setData = "{\"inputName\":\"" + actionSource + "\", \"inputVolumeDb\":" + volume + "}";
			CPH.ObsSendRaw("SetInputVolume", setData);

			jsonString = CPH.ObsSendRaw("GetInputVolume", requestData);
			data = GetData.FromJson(jsonString);

			CPH.SetGlobalVar($"global_{actionSource}_VolumeDB", data.InputVolumeDb);
			CPH.SetGlobalVar($"global_{actionSource}_VolumeMul", data.InputVolumeMul);
		}
		return true;
	}

	public bool VolumeDown()
	{
		CPH.TryGetArg("ActionSource", out string actionSource);
		const int minVolumeDb = -100; // Configurable minimum
		const int volumeStep = 2;     // Configurable step size

		string requestData = "{\"inputName\":\"" + actionSource + "\"}";
		var jsonString = CPH.ObsSendRaw("GetInputVolume", requestData);
		var data = GetData.FromJson(jsonString);

		var volume = Convert.ToInt32(data.InputVolumeDb);
		if (volume > minVolumeDb)
		{
			volume = Math.Max(volume - volumeStep, minVolumeDb);

			string setData = "{\"inputName\":\"" + actionSource + "\", \"inputVolumeDb\":" + volume + "}";
			CPH.ObsSendRaw("SetInputVolume", setData);

			jsonString = CPH.ObsSendRaw("GetInputVolume", requestData);
			data = GetData.FromJson(jsonString);

			CPH.SetGlobalVar($"global_{actionSource}_VolumeDB", data.InputVolumeDb);
			CPH.SetGlobalVar($"global_{actionSource}_VolumeMul", data.InputVolumeMul);
		}
		return true;
	}
    
	public bool VolumeMute()
	{
		CPH.TryGetArg("ActionSource", out string actionSource);
		CPH.TryGetArg("ActionScene", out string actionScene);
		CPH.TryGetArg<int>("FadeSmoothing", out int fadeSmoothing);
		
		// Validate fade smoothing
		if (fadeSmoothing <= 0) fadeSmoothing = 20;
		
		string requestData = "{\"inputName\":\"" + actionSource + "\"}";
		var jsonString = CPH.ObsSendRaw("GetInputVolume", requestData);
		var data = GetData.FromJson(jsonString);

		var volume = Convert.ToInt32(data.InputVolumeDb);
		CPH.SetGlobalVar($"global_PreviousVolume_{actionSource}", volume);
		
		// Calculate steps needed for smoother fade
		int steps = Math.Max(1, (volume + 100) / 2);
		int stepDelay = Math.Max(1, fadeSmoothing);
		
		for (int i = 0; i < steps && volume > -100; i++)
		{
			volume = Math.Max(volume - 2, -100);

			string setData = "{\"inputName\":\"" + actionSource + "\", \"inputVolumeDb\":" + volume + "}";
			CPH.ObsSendRaw("SetInputVolume", setData);

			System.Threading.Thread.Sleep(stepDelay);
		}
		
		// Ensure we reach exactly -100 dB
		if (volume > -100)
		{
			string finalData = "{\"inputName\":\"" + actionSource + "\", \"inputVolumeDb\":-100}";
			CPH.ObsSendRaw("SetInputVolume", finalData);
		}
		
		CPH.ObsSetSourceMuteState(actionScene, actionSource, 0);
		return true;
	}
		
	public bool VolumeUnmute()
	{
		CPH.TryGetArg("ActionSource", out string actionSource);
		CPH.TryGetArg("ActionScene", out string actionScene);
		CPH.TryGetArg<int>("FadeSmoothing", out int fadeSmoothing);

		// Validate fade smoothing
		if (fadeSmoothing <= 0) fadeSmoothing = 20;

		int previousVolume = CPH.GetGlobalVar<int>($"global_PreviousVolume_{actionSource}", true);
		
		// Cap the previous volume at 0 dB maximum and ensure it's not below -100
		previousVolume = Math.Max(-100, Math.Min(previousVolume, 0));
		
		string requestData = "{\"inputName\":\"" + actionSource + "\"}";
		var jsonString = CPH.ObsSendRaw("GetInputVolume", requestData);
		var data = GetData.FromJson(jsonString);

		var volume = Convert.ToInt32(data.InputVolumeDb);
		
		// Unmute first so audio can be heard during fade-in
		CPH.ObsSetSourceMuteState(actionScene, actionSource, 1);
		
		// Calculate steps for smoother fade
		int steps = Math.Max(1, (previousVolume - volume) / 2);
		int stepDelay = Math.Max(1, fadeSmoothing);
		
		for (int i = 0; i < steps && volume < previousVolume; i++)
		{
			volume = Math.Min(volume + 2, previousVolume);

			CPH.LogInfo($"Volume :: {volume}");
			string setData = "{\"inputName\":\"" + actionSource + "\", \"inputVolumeDb\":" + volume + "}";
			CPH.ObsSendRaw("SetInputVolume", setData);

			System.Threading.Thread.Sleep(stepDelay);
		}
		
		// Ensure we reach exactly the target volume
		if (volume < previousVolume)
		{
			string finalData = "{\"inputName\":\"" + actionSource + "\", \"inputVolumeDb\":" + previousVolume + "}";
			CPH.ObsSendRaw("SetInputVolume", finalData);
			CPH.LogInfo($"Final Volume :: {previousVolume}");
		}
		
		return true;
	}
}

public partial class GetData
{
    [JsonProperty("inputVolumeDb")]
    public double InputVolumeDb { get; set; }

    [JsonProperty("inputVolumeMul")]
    public double InputVolumeMul { get; set; }
}

public partial class GetData
{
    public static GetData FromJson(string json) => JsonConvert.DeserializeObject<GetData>(json, Converter.Settings);
}

public static class Serialize
{
    public static string ToJson(this GetData self) => JsonConvert.SerializeObject(self, Converter.Settings);
}

internal static class Converter
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings{MetadataPropertyHandling = MetadataPropertyHandling.Ignore, DateParseHandling = DateParseHandling.None, Converters = {new IsoDateTimeConverter{DateTimeStyles = DateTimeStyles.AssumeUniversal}}, };
}