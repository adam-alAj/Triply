import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/sample_provider.dart';

/// Temporary screen that exists only to make the architecture visible:
/// SampleProvider -> SampleRepository -> ApiClient -> test endpoint.
/// Replace with the real first screen once feature work starts.
class SampleScreen extends StatefulWidget {
  const SampleScreen({super.key});

  @override
  State<SampleScreen> createState() => _SampleScreenState();
}

class _SampleScreenState extends State<SampleScreen> {
  @override
  void initState() {
    super.initState();
    // Deferred so the provider's first notifyListeners() lands after the
    // initial build instead of during it.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<SampleProvider>().loadPosts();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: Theme.of(context).colorScheme.inversePrimary,
        title: const Text('Architecture check'),
        actions: [
          IconButton(
            tooltip: 'Reload',
            onPressed: () => context.read<SampleProvider>().loadPosts(),
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: Consumer<SampleProvider>(
        builder: (context, provider, _) {
          switch (provider.status) {
            case SampleStatus.idle:
            case SampleStatus.loading:
              return const Center(child: CircularProgressIndicator());

            case SampleStatus.failure:
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        provider.errorMessage ?? 'Request failed.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 16),
                      FilledButton(
                        onPressed: provider.loadPosts,
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                ),
              );

            case SampleStatus.success:
              return ListView.separated(
                itemCount: provider.posts.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final post = provider.posts[index];
                  return ListTile(
                    leading: CircleAvatar(child: Text('${post.id}')),
                    title: Text(post.title),
                    subtitle: Text(
                      post.body,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  );
                },
              );
          }
        },
      ),
    );
  }
}
