import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/network/api_client.dart';
import 'data/repositories/sample_repository.dart';
import 'presentation/providers/sample_provider.dart';
import 'presentation/screens/sample_screen.dart';

void main() {
  // Composition root: the one place that knows how the graph is assembled.
  // ApiClient -> Repository -> Provider, all by constructor injection, so any
  // layer can be swapped for a fake in tests.
  final apiClient = ApiClient();
  final sampleRepository = SampleRepository(apiClient: apiClient);

  runApp(MyApp(sampleRepository: sampleRepository));
}

class MyApp extends StatelessWidget {
  const MyApp({super.key, required this.sampleRepository});

  final SampleRepository sampleRepository;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => SampleProvider(repository: sampleRepository),
      child: MaterialApp(
        title: 'Triply',
        theme: ThemeData(
          colorScheme: ColorScheme.fromSeed(seedColor: Colors.deepPurple),
        ),
        home: const SampleScreen(),
      ),
    );
  }
}
