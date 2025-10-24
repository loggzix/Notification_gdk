using UnityEngine;
using DSDK.AnalyticsPlatform;
using DSDK.Logger;
using DSDK.Remote;

public class RemoteConfigMonitor : MonoBehaviour
{
	#region DONT TOUCH

	private static RemoteConfigMonitor _instance;

	public static RemoteConfigMonitor Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = FindFirstObjectByType<RemoteConfigMonitor>();
			}

			return _instance;
		}
	}

	public static bool IsInstanceValid()
	{
		return _instance;
	}

	#region Fields

	/// <summary>
	/// Lấy dữ liệu default từ Monitor thay vì lấy từ constructor
	/// </summary>
	[SerializeField] private bool _getDefaultFromMonitor = false;

	public static bool AllDataLoaded = false;
	#endregion

	#region Main Method

	protected void Awake()
	{
		if (_instance != null && _instance != this)
		{
			Destroy(gameObject);
			return;
		}

		_instance = this;

		if (_getDefaultFromMonitor)
		{
			SetInstance();
		}

		RemoteConfigInstance.TryCall(GetInstance);
	}

	#endregion

	#endregion

	// for monitor only ,not use in runtime

	#region Monitor

	#region Monitor Fields
	[SerializeField] private DebugConfigSDK _debugConfigSDK;
	#endregion

	public void SetInstance()
	{
		DLogger.LogDebug("RemoteConfigMonitor SetInstance", channel: "AnalyticsPlatform");

		#region Get Default
		DebugConfigSDK.SetDefaultValue(_debugConfigSDK);
		#endregion
	}

	public void GetInstance()
	{
		DLogger.LogDebug("RemoteConfigMonitor GetInstance", channel: "AnalyticsPlatform");

		#region Get Instance
		DebugConfigSDK.Instance.ForceFetch();
		#endregion

		RefreshAllInstances();

		AllDataLoaded = true;
	}

	public void RefreshAllInstances()
	{
		#region Refresh Instance
		_debugConfigSDK = DebugConfigSDK.Instance;
		#endregion
	}

	#endregion
}
