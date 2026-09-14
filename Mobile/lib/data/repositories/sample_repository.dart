import '../../core/network/api_client.dart';
import '../models/sample_post.dart';

/// Example repository proving the layering end to end.
///
/// A repository's only job is: call the API through [ApiClient] and map the
/// response to models. No validation, no business rules — those live on the
/// backend (see System Architecture doc).
class SampleRepository {
  SampleRepository({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<List<SamplePost>> fetchPosts({int limit = 10}) async {
    final data = await _apiClient.get<List<dynamic>>(
      '/posts',
      queryParameters: {'_limit': limit},
    );

    return data
        .map((item) => SamplePost.fromJson(item as Map<String, dynamic>))
        .toList(growable: false);
  }
}
