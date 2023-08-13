using Discord.WebSocket;
using OKX.Api;
using OKX.Api.Models.MarketData;
using OKX.Api.Enums;
using ApiSharp.Stream;
using System.Threading.Channels;
using System.Collections.Concurrent;
using OKX.Api.Clients.RestApi;

namespace samb.Services;

public interface IOkxService
{
    ChannelReader<OkxCandlestick> CandlestickChannel { get; }
    ChannelReader<OkxTicker> TickerChannel { get; }
    ChannelReader<OkxIndexTicker> IndexTickerChannel { get; }
    Task SubscribeToMarkCandlesticks(
        string subId,
        string instrument,
        CancellationToken token = default
    );
    Task SubscribeToIndexCandlesticks(
        string subId,
        string instrument,
        CancellationToken token = default
    );
    Task SubscribeToLastCandlesticks(
        string subId,
        string instrument,
        CancellationToken token = default
    );
    Task SubscribeToIndexTicker(string subId, string instrument, CancellationToken token = default);
    Task SubscribeToMarkTicker(string subId, string instrument, CancellationToken token = default);
    Task SubscribeToLastTicker(string subId, string instrument, CancellationToken token = default);
    Task SubscribeToFundingRate(string subId, string instrument, CancellationToken token = default);
    Task<List<string>> GetInstrumentList(CancellationToken token = default);
    Task Unsubscribe(string subId, CancellationToken token = default);
}

public class OkxService : IOkxService, IAsyncDisposable
{
    public ChannelReader<OkxCandlestick> CandlestickChannel => _candleStickChannel.Reader;
    public ChannelReader<OkxTicker> TickerChannel => _tickerChannel.Reader;
    public ChannelReader<OkxIndexTicker> IndexTickerChannel => _indexTickerChannel.Reader;

    private readonly Channel<OkxCandlestick> _candleStickChannel =
        Channel.CreateUnbounded<OkxCandlestick>();
    private readonly Channel<OkxTicker> _tickerChannel = Channel.CreateUnbounded<OkxTicker>();
    private readonly Channel<OkxIndexTicker> _indexTickerChannel =
        Channel.CreateUnbounded<OkxIndexTicker>();
    private readonly ILogger<OkxService> _logger;
    private readonly List<UpdateSubscription> _subs = new();
    private readonly OKXStreamClient _ws;
    private readonly OKXPublicDataRestApiClient _publicData;
    private readonly OKXMarketDataRestApiClient _marketData;
    private readonly ConcurrentDictionary<string, List<UpdateSubscription>> _subIds = new();

    public OkxService(ILogger<OkxService> logger)
    {
        var channelOptions = new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
            Capacity = 5
        };
        _ws = new(); var rest = new OKXRestApiClient();
        _publicData = rest.PublicData;
        _marketData = rest.MarketData;
        _candleStickChannel = Channel.CreateBounded<OkxCandlestick>(channelOptions);
        _logger = logger;
    }

    public async Task SubscribeToMarkCandlesticks(
        string subId,
        string instrument,
        CancellationToken token = default
    )
    {
        void onData(OkxCandlestick data)
        {
            if (data is null)
            {
                _logger.LogInformation("No data");
                return;
            }
            // _logger.LogInformation("Index Candlesticks: {@Data}", data);
            if (!_candleStickChannel.Writer.TryWrite(data))
            {
                _logger.LogInformation("Channel full");
            }
        }
        var sub = await _ws.SubscribeToMarkPriceCandlesticksAsync(
            onData,
            instrument,
            OkxPeriod.OneMinute,
            token
        );
        _ = _subIds.AddOrUpdate(
            subId,
            new List<UpdateSubscription> { sub.Data },
            (_, v) =>
            {
                v.Add(sub.Data);
                return v;
            }
        );
    }

    public async Task SubscribeToIndexCandlesticks(
        string subId,
        string instrument,
        CancellationToken token = default
    )
    {
        void onData(OkxCandlestick data)
        {
            if (data is null)
            {
                _logger.LogInformation("No data");
                return;
            }
            // _logger.LogInformation("Index Candlesticks: {@Data}", data);
            if (!_candleStickChannel.Writer.TryWrite(data))
            {
                _logger.LogInformation("Channel full");
            }
        }
        var sub = await _ws.SubscribeToIndexCandlesticksAsync(
            onData,
            instrument,
            OkxPeriod.OneMinute,
            token
        );
        _ = _subIds.AddOrUpdate(
            subId,
            new List<UpdateSubscription> { sub.Data },
            (_, v) =>
            {
                v.Add(sub.Data);
                return v;
            }
        );
    }

    public async Task SubscribeToLastCandlesticks(
        string subId,
        string instrument,
        CancellationToken token = default
    )
    {
        void onData(OkxCandlestick data)
        {
            if (data is null)
            {
                _logger.LogInformation("No data");
                return;
            }
            // _logger.LogInformation("Index Candlesticks: {@Data}", data);
            if (!_candleStickChannel.Writer.TryWrite(data))
            {
                _logger.LogInformation("Channel full");
            }
        }
        var sub = await _ws.SubscribeToCandlesticksAsync(
            onData,
            instrument,
            OkxPeriod.OneMinute,
            token
        );
        _ = _subIds.AddOrUpdate(
            subId,
            new List<UpdateSubscription> { sub.Data },
            (_, v) =>
            {
                v.Add(sub.Data);
                return v;
            }
        );
    }

    public async Task SubscribeToIndexTicker(
        string subId,
        string instrument,
        CancellationToken token = default
    )
    {
        void onData(OkxIndexTicker data)
        {
            if (data is null)
            {
                _logger.LogInformation("No data");
                return;
            }
            // _logger.LogInformation("Index Candlesticks: {@Data}", data);
            if (!_indexTickerChannel.Writer.TryWrite(data))
            {
                _logger.LogInformation("Channel full");
            }
        }
        var sub = await _ws.SubscribeToIndexTickersAsync(onData, instrument, token);
        _ = _subIds.AddOrUpdate(
            subId,
            new List<UpdateSubscription> { sub.Data },
            (_, v) =>
            {
                v.Add(sub.Data);
                return v;
            }
        );
    }

    public async Task SubscribeToLastTicker(
        string subId,
        string instrument,
        CancellationToken token = default
    )
    {
        void onData(OkxTicker data)
        {
            if (data is null)
            {
                _logger.LogInformation("No data");
                return;
            }
            // _logger.LogInformation("Index Candlesticks: {@Data}", data);
            if (!_tickerChannel.Writer.TryWrite(data))
            {
                _logger.LogInformation("Channel full");
            }
        }
        var sub = await _ws.SubscribeToTickersAsync(onData, instrument, token);
        _ = _subIds.AddOrUpdate(
            subId,
            new List<UpdateSubscription> { sub.Data },
            (_, v) =>
            {
                v.Add(sub.Data);
                return v;
            }
        );
    }

    public Task SubscribeToFundingRate(
        string subId,
        string instrument,
        CancellationToken token = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<List<string>> GetInstrumentList(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public async Task Unsubscribe(string subId, CancellationToken token = default)
    {
        if (_subIds.TryRemove(subId, out var subs))
        {
            await Task.WhenAll(subs.Select(x => _ws.UnsubscribeAsync(x)));
            return;
        }
        throw new InvalidOperationException("Subscription not found");
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(_subIds.Values.SelectMany(x => x).Select(x => _ws.UnsubscribeAsync(x)));
        _ws.Dispose();
        _publicData.Dispose();
        _marketData.Dispose();
    }
}
