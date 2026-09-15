import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../data/models/sample_post.dart';
import '../../data/repositories/sample_repository.dart';

enum SampleStatus { idle, loading, success, failure }

/// Holds UI state for the sample screen. Talks only to [SampleRepository] —
/// never to Dio or [ApiClient] — and the UI talks only to this class.
class SampleProvider extends ChangeNotifier {
  SampleProvider({required SampleRepository repository})
    : _repository = repository;

  final SampleRepository _repository;

  SampleStatus _status = SampleStatus.idle;
  List<SamplePost> _posts = const <SamplePost>[];
  String? _errorMessage;

  SampleStatus get status => _status;
  List<SamplePost> get posts => _posts;
  String? get errorMessage => _errorMessage;

  Future<void> loadPosts() async {
    if (_status == SampleStatus.loading) return;

    _status = SampleStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _posts = await _repository.fetchPosts();
      _status = SampleStatus.success;
    } on ApiException catch (error) {
      _errorMessage = error.message;
      _status = SampleStatus.failure;
    } catch (error) {
      _errorMessage = 'Something went wrong: $error';
      _status = SampleStatus.failure;
    }

    notifyListeners();
  }
}
