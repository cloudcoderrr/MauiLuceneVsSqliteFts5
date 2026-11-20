using MauiBenchmark.Services;

namespace MauiBenchmark;

/// <summary>Minimal page shown in headless CI mode. Runs all benchmarks then exits the process.</summary>
public sealed class AutoRunPage : ContentPage
{
    private int _started;

    public AutoRunPage()
    {
        Title = "FTS Benchmark (CI)";
        var label = new Label
        {
            Text = "Running benchmarks…",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            FontSize = 20,
        };
        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            Children =
            {
                new ActivityIndicator { IsRunning = true, HorizontalOptions = LayoutOptions.Center },
                label,
            },
        };

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        // Give the platform a moment to finish initializing before heavy work.
        await Task.Yield();
        await BenchmarkAppRunner.RunHeadlessAndExitAsync();
    }
}
