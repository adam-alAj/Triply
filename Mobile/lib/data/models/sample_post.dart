/// Throwaway model used only to prove the networking/DI wiring end to end.
///
/// This is NOT a Triply domain model. Delete it once real models exist.
class SamplePost {
  const SamplePost({
    required this.id,
    required this.userId,
    required this.title,
    required this.body,
  });

  factory SamplePost.fromJson(Map<String, dynamic> json) {
    return SamplePost(
      id: json['id'] as int,
      userId: json['userId'] as int,
      title: json['title'] as String? ?? '',
      body: json['body'] as String? ?? '',
    );
  }

  final int id;
  final int userId;
  final String title;
  final String body;
}
