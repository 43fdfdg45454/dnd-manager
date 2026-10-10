import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/ui/infinite_scroll_list.dart';

/// A paged source of numbered rows driving an [InfiniteScrollList].
class _Pager extends StatefulWidget {
  const _Pager({required this.pageSize, required this.total, this.failNext = false});

  final int pageSize;
  final int total;
  final bool failNext;

  @override
  State<_Pager> createState() => _PagerState();
}

class _PagerState extends State<_Pager> {
  late int _count = widget.pageSize;
  late bool _failNext = widget.failNext;
  int loads = 0;

  Future<void> _loadMore() async {
    loads++;
    await Future<void>.delayed(const Duration(milliseconds: 10));
    if (_failNext) {
      _failNext = false;
      throw StateError('sin red');
    }
    setState(() => _count = (_count + widget.pageSize).clamp(0, widget.total));
  }

  @override
  Widget build(BuildContext context) => InfiniteScrollList(
    listKey: const Key('list'),
    itemCount: _count,
    hasMore: _count < widget.total,
    onLoadMore: _loadMore,
    describeError: (error) => 'Error al cargar',
    itemBuilder: (context, i) => SizedBox(height: 50, child: Text('Fila $i', key: Key('row-$i'))),
  );
}

Widget _host(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  testWidgets('pide la página siguiente al acercarse al final', (tester) async {
    await tester.pumpWidget(_host(const _Pager(pageSize: 30, total: 60)));
    await tester.pumpAndSettle();
    final state = tester.state<_PagerState>(find.byType(_Pager));
    expect(state.loads, 0);
    expect(find.byKey(const Key('infinite-scroll-loading')), findsNothing);

    await tester.drag(find.byKey(const Key('list')), const Offset(0, -900));
    await tester.pump();
    expect(state.loads, 1);

    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.byKey(const Key('row-59')), 300);
    expect(find.byKey(const Key('row-59')), findsOneWidget);
    expect(state.loads, 1);
  });

  testWidgets('sigue cargando si la primera página no llena la pantalla', (tester) async {
    await tester.pumpWidget(_host(const _Pager(pageSize: 3, total: 9)));
    await tester.pumpAndSettle();

    final state = tester.state<_PagerState>(find.byType(_Pager));
    expect(state.loads, 2);
    expect(find.byKey(const Key('row-8')), findsOneWidget);
  });

  testWidgets('un fallo muestra el error y "Reintentar" sin reintentar solo', (tester) async {
    await tester.pumpWidget(_host(const _Pager(pageSize: 3, total: 6, failNext: true)));
    await tester.pumpAndSettle();

    final state = tester.state<_PagerState>(find.byType(_Pager));
    expect(state.loads, 1);
    expect(find.text('Error al cargar'), findsOneWidget);
    expect(find.byKey(const Key('infinite-scroll-retry')), findsOneWidget);

    await tester.tap(find.byKey(const Key('infinite-scroll-retry')));
    await tester.pumpAndSettle();
    expect(state.loads, 2);
    expect(find.byKey(const Key('row-5')), findsOneWidget);
    expect(find.byKey(const Key('infinite-scroll-retry')), findsNothing);
  });
}
