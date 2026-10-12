using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Zuemail.MailKit.Abstractions;

namespace Zuemail.MailKit.Services;

/// <summary>Reads summaries and MIME messages using index, UID-range, or search selection.</summary>
public sealed class MailFolderReader : IMailFolderReader
{
    private readonly IEmailReceiver _receiver;
    private readonly ILogger<MailFolderReader> _logger;
    private readonly bool _ownsReceiver;
    private int _skip;
    private int _take = -1;
    private int? _top;
    private UniqueIdRange _range;
    private SearchQuery _query = SearchQuery.All;
    private MessageSummaryItems _items = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope;
    private bool _continueTake;
    private bool _continueRange;
    private ulong _rangeBatchSize;
    private bool _disposed;

    public MailFolderReader(IEmailReceiver receiver, ILogger<MailFolderReader> logger = null, bool ownsReceiver = false)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _logger = logger ?? NullLogger<MailFolderReader>.Instance;
        _ownsReceiver = ownsReceiver;
    }

    public IMailReader Skip(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        _skip = count;
        _top = null;
        return this;
    }

    public IMailReader Take(int count, bool continuous = false)
    {
        if (count < -1)
            throw new ArgumentOutOfRangeException(nameof(count));
        _take = count;
        _continueTake = continuous;
        return this;
    }

    public IMailReader Top(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));
        _top = count;
        _range = null;
        _query = SearchQuery.All;
        return this;
    }

    public IMailReader Range(UniqueId start, UniqueId end, bool continuous = false)
    {
        if (!start.IsValid)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (!end.IsValid)
            throw new ArgumentOutOfRangeException(nameof(end));
        if (end.Id < start.Id)
            throw new ArgumentOutOfRangeException(nameof(end), "The end UID must not precede the start UID.");
        _range = new UniqueIdRange(start, end);
        _continueRange = continuous;
        _rangeBatchSize = (ulong)end.Id - start.Id + 1;
        _top = null;
        _continueTake = false;
        return this;
    }

    public IMailReader Range(UniqueId start, int batchSize = 0, bool continuous = true)
    {
        if (!start.IsValid)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (batchSize < 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        var endId = batchSize == 0 ? uint.MaxValue : Math.Min(uint.MaxValue, (ulong)start.Id + (uint)batchSize - 1);
        _range = new UniqueIdRange(start, new UniqueId((uint)endId));
        _continueRange = continuous;
        _rangeBatchSize = batchSize == 0 ? (ulong)uint.MaxValue - start.Id + 1 : (uint)batchSize;
        _top = null;
        _continueTake = false;
        return this;
    }

    public IMailReader Query(SearchQuery query)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _range = null;
        _top = null;
        _continueRange = false;
        _continueTake = false;
        return this;
    }

    public IMailReader Items(MessageSummaryItems items)
    {
        _items = items | MessageSummaryItems.UniqueId;
        return this;
    }

    public async Task<IList<IMessageSummary>> GetMessageSummariesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_take == 0)
            return new List<IMessageSummary>();
        var folder = await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        var closeWhenFinished = !folder.IsOpen;
        try
        {
            if (!folder.IsOpen)
                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
            var summaries = await SelectSummariesAsync(folder, cancellationToken).ConfigureAwait(false);
            AdvanceSelection(summaries);
            return summaries;
        }
        finally
        {
            if (closeWhenFinished && folder.IsOpen)
                await folder.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<IList<MimeMessage>> GetMimeMessagesAsync(CancellationToken cancellationToken = default,
        ITransferProgress transferProgress = null)
    {
        ThrowIfDisposed();
        if (_take == 0)
            return new List<MimeMessage>();
        var folder = await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        var closeWhenFinished = !folder.IsOpen;
        var result = new List<MimeMessage>();
        try
        {
            if (closeWhenFinished)
                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
            var summaries = await SelectSummariesAsync(folder, cancellationToken).ConfigureAwait(false);
            AdvanceSelection(summaries);
            result.Capacity = summaries.Count;
            foreach (var summary in summaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(await summary.Folder.GetMessageAsync(summary.UniqueId, cancellationToken, transferProgress).ConfigureAwait(false));
            }
            return result;
        }
        catch
        {
            foreach (var message in result)
                message.Dispose();
            throw;
        }
        finally
        {
            if (closeWhenFinished && folder.IsOpen)
                await folder.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<IList<IMessageSummary>> SelectSummariesAsync(IMailFolder folder, CancellationToken cancellationToken)
    {
        if (_range != null)
        {
            var ranged = await folder.FetchAsync(_range, _items, cancellationToken).ConfigureAwait(false);
            return Limit(ranged.OrderBy(item => item.UniqueId.Id).ToList());
        }
        if (_query != SearchQuery.All)
        {
            var matches = await folder.SearchAsync(_query, cancellationToken).ConfigureAwait(false);
            var ordered = matches.OrderByDescending(uid => uid.Id).Skip(_skip);
            if (_take >= 0)
                ordered = ordered.Take(_take);
            var ids = ordered.OrderBy(uid => uid.Id).ToList();
            return ids.Count == 0 ? new List<IMessageSummary>() :
                await folder.FetchAsync(ids, _items, cancellationToken).ConfigureAwait(false);
        }
        if (_top.HasValue)
        {
            var start = Math.Max(0, folder.Count - _top.Value);
            var end = Math.Max(0, folder.Count - 1);
            var latest = await folder.FetchAsync(start, end, _items, cancellationToken).ConfigureAwait(false);
            return latest.OrderByDescending(item => item.Index).ToList();
        }

        var lastIndex = _take < 0 ? folder.Count - 1 :
            (int)Math.Min(folder.Count - 1L, (long)_skip + _take - 1);
        if (folder.Count == 0 || lastIndex < _skip)
            return new List<IMessageSummary>();
        return await folder.FetchAsync(_skip, lastIndex, _items, cancellationToken).ConfigureAwait(false);
    }

    private IList<IMessageSummary> Limit(IList<IMessageSummary> summaries)
    {
        IEnumerable<IMessageSummary> selected = summaries;
        if (_skip > 0)
            selected = selected.Skip(_skip);
        if (_take >= 0)
            selected = selected.Take(_take);
        return selected.ToList();
    }

    private void AdvanceSelection(IList<IMessageSummary> summaries)
    {
        if (summaries.Count == 0)
            return;
        if (_continueRange && _range != null)
        {
            var nextStart = (ulong)summaries.Max(summary => summary.UniqueId.Id) + 1;
            var nextEnd = nextStart + _rangeBatchSize - 1;
            if (nextStart > uint.MaxValue || nextEnd > uint.MaxValue)
                _range = null;
            else
                _range = new UniqueIdRange(new UniqueId((uint)nextStart), new UniqueId((uint)nextEnd));
        }
        else if (_continueTake && _take > 0)
        {
            _skip = checked(_skip + _take);
        }
    }

    public IMailFolderReader Copy()
    {
        ThrowIfDisposed();
        var clone = new MailFolderReader(_receiver.Clone(), _logger, ownsReceiver: true)
        {
            _skip = _skip,
            _take = _take,
            _top = _top,
            _range = _range,
            _query = _query,
            _items = _items,
            _continueTake = _continueTake,
            _continueRange = _continueRange,
            _rangeBatchSize = _rangeBatchSize
        };
        return clone;
    }

    public override string ToString() => $"{_receiver} (skip {_skip}, take {_take})";

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_ownsReceiver)
            await _receiver.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_ownsReceiver)
            _receiver.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MailFolderReader));
    }
}