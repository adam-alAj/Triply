import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/destination_assets_cache.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../widgets/empty_state.dart';
import '../../widgets/error_state.dart';

/// Profile → "ABOUT TRIPLY" links. Terms and Privacy are static in-app text
/// (no hosted legal pages yet); the destinations directory is the live
/// `GET /api/destinations` list.

class TermsOfServiceScreen extends StatelessWidget {
  const TermsOfServiceScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const _TextPage(
      title: 'Terms of Service',
      sections: [
        ('Using Triply', 'Triply helps you plan trips with AI-generated itineraries and cost estimates. You are responsible for the bookings and travel decisions you make.'),
        ('Estimates, not quotes', 'All prices shown are estimates based on curated reference prices. Actual costs may differ; always confirm prices with the provider before paying.'),
        ('Your account', 'Keep your login details private. You can edit or delete your trips at any time.'),
        ('Changes', 'We may update these terms as Triply evolves. Continued use of the app means you accept the updated terms.'),
      ],
    );
  }
}

class PrivacyPolicyScreen extends StatelessWidget {
  const PrivacyPolicyScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const _TextPage(
      title: 'Privacy Policy & GDPR',
      sections: [
        ('What we store', 'Your name, email, trip details and preferences — only what is needed to plan and save your trips.'),
        ('How it is used', 'Trip details are sent to our AI planner to generate itineraries. We do not sell your data.'),
        ('Your rights', 'Under GDPR you can access, correct, export or delete your personal data. Contact the Triply team to make a request.'),
        ('Security', 'Your session is stored securely on your device and all traffic to our servers is encrypted.'),
      ],
    );
  }
}

class SupportedDestinationsScreen extends StatefulWidget {
  const SupportedDestinationsScreen({super.key});

  @override
  State<SupportedDestinationsScreen> createState() =>
      _SupportedDestinationsScreenState();
}

class _SupportedDestinationsScreenState
    extends State<SupportedDestinationsScreen> {
  late Future<List<Map<String, dynamic>>> _future = _load();

  Future<List<Map<String, dynamic>>> _load() =>
      DestinationAssetsCache.instance.getDestinations(context.read<ApiClient>());

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: Text('Supported Destinations', style: AppTextStyles.headlineSm),
        backgroundColor: AppColors.surface,
      ),
      body: FutureBuilder<List<Map<String, dynamic>>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(
              child: ErrorState(
                title: 'Unable to load destinations',
                description: 'Check your connection and try again.',
                onAction: () => setState(() => _future = _load()),
              ),
            );
          }

          final destinations = snapshot.data ?? const [];
          if (destinations.isEmpty) {
            return const Center(
              child: EmptyState(
                icon: Icons.map_outlined,
                title: 'No destinations yet',
                description: 'Supported destinations will appear here.',
              ),
            );
          }

          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: destinations.length,
            separatorBuilder: (_, _) => const SizedBox(height: 8),
            itemBuilder: (context, index) {
              final destination = destinations[index];
              return Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.place_outlined, color: AppColors.primary),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        destination['name'] as String? ?? '',
                        style: AppTextStyles.labelLg,
                      ),
                    ),
                    Text(
                      destination['countryName'] as String? ?? '',
                      style: AppTextStyles.bodySm,
                    ),
                  ],
                ),
              );
            },
          );
        },
      ),
    );
  }
}

class _TextPage extends StatelessWidget {
  const _TextPage({required this.title, required this.sections});

  final String title;
  final List<(String, String)> sections;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: Text(title, style: AppTextStyles.headlineSm),
        backgroundColor: AppColors.surface,
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          for (final (heading, body) in sections) ...[
            Text(heading, style: AppTextStyles.labelLg),
            const SizedBox(height: 6),
            Text(body, style: AppTextStyles.bodyMd),
            const SizedBox(height: 20),
          ],
        ],
      ),
    );
  }
}
