using Microsoft.Extensions.DependencyInjection;
using MauiBenchmark.Services;

namespace MauiBenchmark;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		if (PlatformSupport.AutoRun)
		{
			// Headless CI mode: show a minimal page and run everything, then exit.
			return new Window(new AutoRunPage());
		}

		return new Window(new AppShell());
	}
}