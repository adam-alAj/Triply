// Architecture smoke test: swaps the repository for a fake to prove the
// Provider -> Repository seam is injectable, and that the UI renders both the
// success and failure states it receives from the provider.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:triply_project/core/network/api_client.dart';
import 'package:triply_project/data/models/sample_post.dart';
import 'package:triply_project/data/repositories/sample_repository.dart';
import 'package:triply_project/presentation/providers/sample_provider.dart';
import 'package:triply_project/presentation/screens/sample_screen.dart';

class _FakeSampleRepository implements SampleRepository {
  _FakeSampleRepository({this.posts = const [], this.error});

  final List<SamplePost> posts;
  final ApiException? error;

  @override
  Future<List<SamplePost>> fetchPosts({int limit = 10}) async {
    final failure = error;
    if (failure != null) throw failure;
    return posts;
  }
}

Widget _wrap(SampleRepository repository) {
  return ChangeNotifierProvider(
    create: (_) => SampleProvider(repository: repository),
    child: const MaterialApp(home: SampleScreen()),
  );
}

void main() {
  testWidgets('renders posts returned through the provider', (tester) async {
    final repository = _FakeSampleRepository(
      posts: const [
        SamplePost(id: 1, userId: 1, title: 'first post', body: 'body one'),
        SamplePost(id: 2, userId: 1, title: 'second post', body: 'body two'),
      ],
    );

    await tester.pumpWidget(_wrap(repository));
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pump();
    await tester.pump();

    expect(find.text('first post'), findsOneWidget);
    expect(find.text('second post'), findsOneWidget);
  });

  testWidgets('surfaces repository failures as an error state', (tester) async {
    final repository = _FakeSampleRepository(
      error: ApiException('The request timed out.'),
    );

    await tester.pumpWidget(_wrap(repository));
    await tester.pump();
    await tester.pump();

    expect(find.text('The request timed out.'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });
}
