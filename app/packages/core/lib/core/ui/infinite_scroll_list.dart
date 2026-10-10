import 'package:flutter/material.dart';

import '../network/api_error.dart';

/// Distance to the end of the list (in logical pixels) at which the next page
/// is requested.
const double infiniteScrollPrefetchExtent = 300;

/// A `ListView.builder` that asks for the next page by itself: when the user
/// scrolls within [prefetchExtent] of the end, and also right after a page
/// that does not fill the viewport. While a page loads, a progress row sits
/// at the end of the list (an empty row of the same height otherwise, so the
/// list does not jump); when it fails, the error shows in a SnackBar
/// ([describeError]) and the last row offers "Reintentar" (no automatic retry,
/// so a failing server is not hammered).
///
/// [onLoadMore] must append the next page (and leave [hasMore] false at the
/// end); it is never called twice at the same time.
class InfiniteScrollList extends StatefulWidget {
  const InfiniteScrollList({
    super.key,
    required this.itemCount,
    required this.itemBuilder,
    required this.hasMore,
    required this.onLoadMore,
    this.listKey,
    this.padding,
    this.physics,
    this.controller,
    this.prefetchExtent = infiniteScrollPrefetchExtent,
    this.describeError = describeApiError,
  });

  final int itemCount;
  final IndexedWidgetBuilder itemBuilder;
  final bool hasMore;
  final Future<void> Function() onLoadMore;

  /// Key of the inner `ListView` (for tests and scroll restoration).
  final Key? listKey;
  final EdgeInsetsGeometry? padding;
  final ScrollPhysics? physics;

  /// Optional external controller; an internal one is used otherwise.
  final ScrollController? controller;
  final double prefetchExtent;
  final String Function(Object error) describeError;

  @override
  State<InfiniteScrollList> createState() => _InfiniteScrollListState();
}

class _InfiniteScrollListState extends State<InfiniteScrollList> {
  ScrollController? _ownController;
  bool _loading = false;
  bool _failed = false;

  /// Item count when the viewport check last asked for a page: it asks again
  /// only once the list has grown, so a no-op load cannot loop frame by frame.
  int? _viewportLoadAt;

  ScrollController get _controller => widget.controller ?? (_ownController ??= ScrollController());

  @override
  void initState() {
    super.initState();
    _controller.addListener(_onScroll);
  }

  @override
  void didUpdateWidget(InfiniteScrollList oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.controller != widget.controller) {
      (oldWidget.controller ?? _ownController)?.removeListener(_onScroll);
      _controller.addListener(_onScroll);
    }
    // New data (or a new search) clears a previous failure.
    if (oldWidget.itemCount != widget.itemCount) _failed = false;
  }

  @override
  void dispose() {
    _controller.removeListener(_onScroll);
    _ownController?.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (!_controller.hasClients) return;
    if (_controller.position.extentAfter < widget.prefetchExtent) _maybeLoad();
  }

  void _maybeLoad() {
    if (_loading || _failed || !widget.hasMore) return;
    _load();
  }

  Future<void> _load() async {
    final messenger = ScaffoldMessenger.maybeOf(context);
    setState(() {
      _loading = true;
      _failed = false;
    });
    try {
      await widget.onLoadMore();
    } catch (error) {
      if (!mounted) return;
      _failed = true;
      messenger
        ?..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(widget.describeError(error))));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  /// A short page may not fill the viewport, so nothing could scroll: load on.
  void _checkViewport() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_controller.hasClients) return;
      final position = _controller.position;
      if (!position.hasContentDimensions) return;
      if (position.extentAfter >= widget.prefetchExtent) return;
      if (_viewportLoadAt == widget.itemCount) return;
      _viewportLoadAt = widget.itemCount;
      _maybeLoad();
    });
  }

  @override
  Widget build(BuildContext context) {
    if (widget.hasMore) _checkViewport();
    final footer = widget.hasMore || _loading;
    return ListView.builder(
      key: widget.listKey,
      controller: _controller,
      padding: widget.padding,
      physics: widget.physics,
      itemCount: widget.itemCount + (footer ? 1 : 0),
      itemBuilder: (context, index) {
        if (index < widget.itemCount) return widget.itemBuilder(context, index);
        return Padding(
          key: const Key('infinite-scroll-footer'),
          padding: const EdgeInsets.all(16),
          child: Center(
            child: _loading
                ? const SizedBox.square(
                    dimension: 28,
                    child: CircularProgressIndicator(
                      key: Key('infinite-scroll-loading'),
                      strokeWidth: 3,
                    ),
                  )
                : _failed
                ? TextButton.icon(
                    key: const Key('infinite-scroll-retry'),
                    onPressed: _load,
                    icon: const Icon(Icons.refresh),
                    label: const Text('Reintentar'),
                  )
                : const SizedBox(height: 28),
          ),
        );
      },
    );
  }
}
