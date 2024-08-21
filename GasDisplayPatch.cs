using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StationeersMods.Interface;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;

namespace ExamplePatchMod
{
	enum PatchGasDisplayMode
	{
		Pressure,
		Temperature,
		TemperatureKelvin,
	}

	[HarmonyPatch(typeof(GasDisplay))]
    [HarmonyPatch("ButtonToggleMode")]
	public class ButtonTogglePatch
	{
		static bool Prefix(GasDisplay __instance)
		{
			__instance.Flag++;
			if (__instance.Flag > 2)
			{
				__instance.Flag = 0;
			}
			Motherboard.UseComputer(3, __instance.ReferenceId, __instance.ReferenceId, __instance.Flag, true, "");
			return false;
		}
	}

	[HarmonyPatch(typeof(GasDisplay))]
	[HarmonyPatch("SetFlag")]
	public class SetFlagPatch {

		[HarmonyReversePatch]
		public static void SetFlag(Motherboard instance, int flag)
		{
			instance.SetFlag(flag);
		}

		private static bool Prefix(int page, GasDisplay __instance, ref int ____lastUnitIndex, string[] ____displayUnits)
		{
			SetFlag(__instance, page);
			__instance.Flag = page;
			if (page == 0)
			{
				__instance.DisplayMode = GasDisplayMode.Pressure;
				__instance.DisplayTitle.text = "PRESSURE";
				__instance.DisplayUnits.text = "kPa";
				____lastUnitIndex = Array.IndexOf<string>(____displayUnits, __instance.DisplayUnits.text);
				__instance.ToggleModeButtonText.text = "Mode: <b>Pressure</b>";
				// this code doesnt work for some reason
				//Thing.Event displayModeType = DisplayModeType;
				//if (displayModeType == null)
				//{
				//	return false;
				//}
				//displayModeType();
				return false;
			}
			else if (page == 1)
			{
				__instance.DisplayMode = GasDisplayMode.Temperature;
				__instance.DisplayTitle.text = "TEMPERATURE";
				__instance.DisplayUnits.text = "°C";
				__instance.ToggleModeButtonText.text = "Mode: <b>Temperature (°C)</b>";
				// this code doesnt work for some reason
				//Thing.Event displayModeType2 = DisplayModeType;
				//if (displayModeType2 == null)
				//{
				//	return false;
				//}
				//displayModeType2();
				return false;
			}
			else
			{
				__instance.DisplayMode = GasDisplayMode.Temperature; //TODO
				__instance.DisplayTitle.text = "TEMPERATURE";
				__instance.DisplayUnits.text = "K";
				__instance.ToggleModeButtonText.text = "Mode: <b>Temperature (K)</b>";
				return false;
			}
		}
	}


	[HarmonyPatch(typeof(GasDisplay))]
	[HarmonyPatch("OnThreadUpdate")]
	class OnThreadUpdatePatch
	{
		public static List<Device> LinkedDevices(Motherboard instance)
		{
			return instance.LinkedDevices;
		}

		static bool Prefix(GasDisplay __instance, ref float ____temperature, ref float ____pressure, ref int ____sensors, ref string ____displayText, ref bool ____notANumber, ref float ____displayPressure)
		{
			var shouldDraw = AccessTools.Method(typeof(GasDisplay), "ShouldDraw");
			var errorCheckFromThread = AccessTools.Method(typeof(GasDisplay), "ErrorCheckFromThread");

			List<Device> linkedDevices = LinkedDevices(__instance);

			if (GameManager.IsBatchMode)
			{
				return false;
			}
			lock (linkedDevices)
			{
				if ((bool)shouldDraw.Invoke(__instance, null))
				{
					if (linkedDevices.Count == 0)
					{
						____displayText = "-";
					}
					else
					{
						____sensors = 0;
						GasDisplayMode displayMode = __instance.DisplayMode;
						if (displayMode == GasDisplayMode.Temperature)
						{
							____temperature = 0f;
							int count = __instance.GasSensors.Count;
							while (count-- > 0)
							{
								GasSensor gasSensor = __instance.GasSensors[count];
								if (gasSensor && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && __instance.IsDeviceConnected(gasSensor) && gasSensor.AirTemperature >= 0f)
								{
									____temperature += gasSensor.AirTemperature;
									____sensors++;
								}
							}
							int count2 = __instance.PipeAnalysizers.Count;
							while (count2-- > 0)
							{
								PipeAnalysizer pipeAnalysizer = __instance.PipeAnalysizers[count2];
								if (pipeAnalysizer && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && __instance.IsDeviceConnected(pipeAnalysizer) && pipeAnalysizer.PipeTemperature >= 0f)
								{
									____temperature += pipeAnalysizer.PipeTemperature;
									____sensors++;
								}
							}
							int count3 = __instance.GasTankStorages.Count;
							while (count3-- > 0)
							{
								GasTankStorage gasTankStorage = __instance.GasTankStorages[count3];
								if (gasTankStorage && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && __instance.IsDeviceConnected(gasTankStorage) && gasTankStorage.TankTemperature >= 0f)
								{
									____temperature += gasTankStorage.TankTemperature;
									____sensors++;
								}
							}
							int count4 = __instance.Structures.Count;
							while (count4-- > 0)
							{
								Structure structure = __instance.Structures[count4];
								if (structure && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && structure.InternalAtmosphere != null && structure.InternalAtmosphere.Temperature >= 0f)
								{
									____temperature += structure.InternalAtmosphere.Temperature;
									____sensors++;
								}
							}
							____temperature /= (float)____sensors;
							if (float.IsNaN(____temperature))
							{
								____displayText = "NAN";
								if (!____notANumber)
								{
									____notANumber = true;
									var error_check = (UniTaskVoid)errorCheckFromThread.Invoke(__instance, null);
									error_check.Forget();
								}
							}
							else
							{
								string format = "F1";
								if (__instance.DisplayUnits.text == "K")
								{
									if (____temperature >= 1000f)
									{
										format = "F0";
									}
									____displayText = ____temperature.ToString(format);
								}
								else
								{
									float num = ____temperature - 273.15f;
									if (num >= 1000f)
									{
										format = "F0";
									}
									____displayText = ((____temperature <= 1f) ? "-" : num.ToString(format));
								}
								if (____notANumber)
								{
									____notANumber = false;
									var error_check = (UniTaskVoid)errorCheckFromThread.Invoke(__instance, null);
									error_check.Forget();
								}
							}
						} 
						else 
						{
							____pressure = 0f;
							int count5 = __instance.GasSensors.Count;
							while (count5-- > 0)
							{
								GasSensor gasSensor2 = __instance.GasSensors[count5];
								if (gasSensor2 && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && __instance.IsDeviceConnected(gasSensor2) && gasSensor2.AirPressure >= 0f)
								{
									____pressure += gasSensor2.AirPressure;
									____sensors++;
								}
							}
							int count6 = __instance.PipeAnalysizers.Count;
							while (count6-- > 0)
							{
								PipeAnalysizer pipeAnalysizer2 = __instance.PipeAnalysizers[count6];
								if (pipeAnalysizer2 && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && __instance.IsDeviceConnected(pipeAnalysizer2) && pipeAnalysizer2.PipePressure >= 0f)
								{
									____pressure += pipeAnalysizer2.PipePressure;
									____sensors++;
								}
							}
							int count7 = __instance.GasTankStorages.Count;
							while (count7-- > 0)
							{
								GasTankStorage gasTankStorage2 = __instance.GasTankStorages[count7];
								if (gasTankStorage2 && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && __instance.IsDeviceConnected(gasTankStorage2) && gasTankStorage2.TankPressure >= 0f)
								{
									____pressure += gasTankStorage2.TankPressure;
									____sensors++;
								}
							}
							int count8 = __instance.Structures.Count;
							while (count8-- > 0)
							{
								Structure structure2 = __instance.Structures[count8];
								if (!(structure2 == null) && __instance.ParentComputer != null && __instance.ParentComputer.DataCableNetwork != null && structure2.InternalAtmosphere != null && structure2.InternalAtmosphere.PressureGassesAndLiquidsInPa >= 0f)
								{
									____pressure += (structure2.InternalAtmosphere.PressureGassesAndLiquidsInPa/1000);
									____sensors++;
								}
							}
							____pressure /= (float)____sensors;
							if (float.IsNaN(____pressure))
							{
								____displayText = "NAN";
								if (!____notANumber)
								{
									____notANumber = true;
									var error_check = (UniTaskVoid)errorCheckFromThread.Invoke(__instance, null);
									error_check.Forget();
								}
							}
							else
							{
								____displayPressure = Mathf.Lerp(____displayPressure, ____pressure, __instance.LerpSpeed);
								____displayText = __instance.FormatDisplayPressure(____displayPressure);
								if (____notANumber)
								{
									____notANumber = false;
									var error_check = (UniTaskVoid)errorCheckFromThread.Invoke(__instance, null);
									error_check.Forget();
								}
							}
						}
					}
				}
			}
			return false;
		}
	}
}
