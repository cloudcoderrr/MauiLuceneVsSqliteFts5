using Microsoft.Extensions.Logging;

namespace MauiBenchmark;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		Benchmarks.Core.SqliteInit.Ensure();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
