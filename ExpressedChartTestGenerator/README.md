# ExpressedChartTestGenerator

`ExpressedChartTestGenerator` is an application to generate test classes for [ChartGeneratorTests](../ChartGeneratorTests/README.md) for asserting that a song file's [ExpressedChart](../StepManiaLibrary/docs/ExpressedChart.md) representation matches expectations.

The expected workflow is to first write a test `sm` or `ssc` chart, verify the chart's [ExpressedChart](../StepManiaLibrary/docs/ExpressedChart.md) matches expectations, then copy the chart's song folder to `ChartGeneratorTests\TestData`, and then run `ExpressedChartTestGenerator` to generate a test class for it.

To verify a chart's `ExpressedChart` matches expectations consider using [StepManiaChartGenerator](https://github.com/PerryAsleep/StepManiaChartGenerator) with `OutputVisualizations` set to `true` (see [Config](https://github.com/PerryAsleep/StepManiaChartGenerator/blob/main/StepManiaChartGenerator/docs/Config.md) and then examining the [Visualization](https://github.com/PerryAsleep/StepManiaChartGenerator/blob/main/StepManiaChartGenerator/docs/Visualizations.md) for the test file.

## Usage
```
Usage:
  ExpressedChartTestGenerator [options] <argument>

Arguments:
  <argument>    Name of the folder containing the sm file to use for the test.
                 Expected to be in ChartGeneratorTests\TestData.

Options:
  --file <file>                Name of the song file without extension inside of the given folder to use for the test. [default: test]
  --extension <extension>      Extension of the song file to use for the test. [default: sm]
  --type <type>                ChartType of the chart within the sm file to test. [default: dance-single]
  --difficulty <difficulty>    DifficultyType of the chart within the sm file to test. [default: Beginner]
  --full-file                  Write a new cs file containing the full test for the chart.
                                Otherwise log the test as a method to the console to be copied. [default: False]
  --version                    Show version information
  -?, -h, --help               Show help and usage information
```
## Example Usage
```
ExpressedChartTestGenerator.exe "GIGA VIOLATE" --file="GIGA VIOLATE" --extension=sm --type=dance-single --difficulty=Challenge --full-file=True
```
This will generate `TestGIGAVIOLATE.cs` in the `ChartGeneratorTests` directory. `TestGIGAVIOLATE.cs` must then be added to the `ChartGeneratorTests` project.