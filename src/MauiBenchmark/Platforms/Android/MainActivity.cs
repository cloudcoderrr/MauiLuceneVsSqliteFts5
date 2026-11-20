using Android.App;
using Android.Content.PM;
using Android.OS;
using MauiBenchmark.Services;

namespace MauiBenchmark;

[Activity(Name = "com.companyname.mauibenchmark.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		CaptureExtras();
		base.OnCreate(savedInstanceState);
	}

	private void CaptureExtras()
	{
		var extras = Intent?.Extras;
		if (extras is null)
		{
			return;
		}

		foreach (var key in extras.KeySet() ?? Enumerable.Empty<string>())
		{
			if (key is null || !key.StartsWith("BENCH_", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var value = extras.Get(key)?.ToString();
			if (value is not null)
			{
				PlatformSupport.LaunchExtras[key] = value;
			}
		}
	}
}
