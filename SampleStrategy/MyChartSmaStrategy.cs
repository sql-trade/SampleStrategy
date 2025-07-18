using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.DataFeedsCore;
using ATAS.Indicators;
using ATAS.Indicators.Technical;
using ATAS.Strategies.Chart;
using OFT.Localization;
using Utils.Common.Logging;

namespace SampleStrategy;

[DisplayName("Test strategy")]
public class SmaChartStrategy : ChartStrategy
{
    #region ctor

    public SmaChartStrategy()
    {
        var firstSeries = (ValueDataSeries)DataSeries[0];
        firstSeries.Name = "Short";
        firstSeries.Color = Colors.Red;
        firstSeries.VisualType = VisualMode.Line;

        DataSeries.Add(new ValueDataSeries("Long")
        {
            VisualType = VisualMode.Line,
            Color = Colors.Green
        });

        ShortPeriod = 21;
        LongPeriod = 75;
        Volume = 1;
        ClosePositionOnStopping = true;
    }

    #endregion

    #region Private fields

    private readonly SMA _shortSma = new();
    private readonly SMA _longSma = new();

    private int _lastBar;

    #endregion

    #region Public properties

    [Display(
        Name = "Short period",
        Order = 10)]
    [Parameter]
    public int ShortPeriod
    {
        get => _shortSma.Period;
        set
        {
            _shortSma.Period = Math.Max(1, value);
            RecalculateValues();
        }
    }

    [Display(
        Name = "Long period",
        Order = 20)]
    [Parameter]
    public int LongPeriod
    {
        get => _longSma.Period;
        set
        {
            _longSma.Period = Math.Max(1, value);
            RecalculateValues();
        }
    }

    [Display(
        Name = "Volume",
        Order = 30)]
    [Parameter]
    public decimal Volume { get; set; }

    [Display(ResourceType = typeof(Strings),
        Name = "ClosePositionOnStopping",
        Order = 40)]
    [Parameter]
    public bool ClosePositionOnStopping { get; set; }

    #endregion

    #region Overrides of BaseIndicator

    protected override void OnCalculate(int bar, decimal value)
    {
        var shortSma = _shortSma.Calculate(bar, value);
        var longSma = _longSma.Calculate(bar, value);

        DataSeries[0][bar] = shortSma;
        DataSeries[1][bar] = longSma;

        var prevBar = _lastBar;
        _lastBar = bar;

        if (!CanProcess(bar) || prevBar == bar)
            return;

        if (_shortSma[prevBar - 1] < _longSma[prevBar - 1] && _shortSma[prevBar] >= _longSma[prevBar])
            //cross up
            OpenPosition(OrderDirections.Buy);

        if (_shortSma[prevBar - 1] > _longSma[prevBar - 1] && _shortSma[prevBar] <= _longSma[prevBar])
            //cross down
            OpenPosition(OrderDirections.Sell);
    }

    protected override void OnStopping()
    {
        if (CurrentPosition != 0 && ClosePositionOnStopping)
        {
            RaiseShowNotification($"Closing current position {CurrentPosition} on stopping.",
                level: LoggingLevel.Warning);
            CloseCurrentPosition();
        }

        base.OnStopping();
    }

    #endregion

    #region Private methods

    private void OpenPosition(OrderDirections direction)
    {
        var order = new Order
        {
            Portfolio = Portfolio,
            Security = Security,
            Direction = direction,
            Type = OrderTypes.Market,
            QuantityToFill = GetOrderVolume()
        };

        OpenOrder(order);
    }

    private void CloseCurrentPosition()
    {
        var order = new Order
        {
            Portfolio = Portfolio,
            Security = Security,
            Direction = CurrentPosition > 0 ? OrderDirections.Sell : OrderDirections.Buy,
            Type = OrderTypes.Market,
            QuantityToFill = Math.Abs(CurrentPosition)
        };

        OpenOrder(order);
    }

    private decimal GetOrderVolume()
    {
        if (CurrentPosition == 0)
            return Volume;

        if (CurrentPosition > 0)
            return Volume + CurrentPosition;

        return Volume + Math.Abs(CurrentPosition);
    }

    #endregion
}