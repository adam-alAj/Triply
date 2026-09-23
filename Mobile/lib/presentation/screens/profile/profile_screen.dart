import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/user_settings_data.dart';
import '../../../data/repositories/api_user_settings_repository.dart';
import '../../providers/auth_provider.dart';
import '../../providers/user_settings_provider.dart';
import '../../widgets/app_bottom_navigation.dart';
import '../../widgets/notifications_sheet.dart';
import 'about_screens.dart';

/// MOB-PROF-01 — Profile (UI Pages §2, §9): "Display name, email, logout".
/// "Edit name" and "Log Out" are the task's acceptance criteria; preferences
/// and stats are now wired to the real backend too (GET/PUT /api/users/me/
/// preferences, GET /api/users/me/stats). AI Curation & Privacy toggles have
/// no backend field yet and are remembered on-device only.
class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => UserSettingsProvider(
        repository:
            ApiUserSettingsRepository(apiClient: context.read<ApiClient>()),
      ),
      child: const _ProfileView(),
    );
  }
}

class _ProfileView extends StatelessWidget {
  const _ProfileView();

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final settings = context.watch<UserSettingsProvider>();

    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
          children: [
            const _TopBar(),
            const SizedBox(height: 16),
            _ProfileCard(
              name: user?.name ?? 'Traveler',
              email: user?.email ?? '',
              stats: settings.stats,
            ),
            const SizedBox(height: 20),
            const _SectionLabel('TRIP PREFERENCES & LOCALIZATION'),
            const SizedBox(height: 8),
            _PreferencesCard(settings: settings),
            const SizedBox(height: 20),
            const _SectionLabel('AI CURATION & PRIVACY'),
            const SizedBox(height: 8),
            const _PrivacyCard(),
            const SizedBox(height: 20),
            const _SectionLabel('ABOUT TRIPLY'),
            const SizedBox(height: 8),
            const _AboutCard(),
            const SizedBox(height: 12),
            Center(
              child: Text(
                'Triply v1.0.4 (Production Mobile)',
                style: AppTextStyles.labelSm,
              ),
            ),
            const SizedBox(height: 16),
            _LogOutButton(),
          ],
        ),
      ),
      bottomNavigationBar: const AppBottomNavigation(selected: AppNavTab.profile),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar();

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Image.asset('assets/images/Triply Logo.png', width: 24, height: 24),
        const SizedBox(width: 8),
        Text('Triply', style: AppTextStyles.labelLg.copyWith(fontWeight: FontWeight.w700)),
        const Spacer(),
        IconButton(
          tooltip: 'Notifications',
          onPressed: () => showNotificationsSheet(context),
          icon: const Icon(Icons.notifications_none_outlined, size: 21),
        ),
        Container(
          width: 30,
          height: 30,
          decoration: const BoxDecoration(color: AppColors.primary, shape: BoxShape.circle),
          alignment: Alignment.center,
          child: const Icon(Icons.person, color: Colors.white, size: 16),
        ),
      ],
    );
  }
}

class _ProfileCard extends StatelessWidget {
  const _ProfileCard({required this.name, required this.email, this.stats});

  final String name;
  final String email;
  final UserStatsData? stats;

  @override
  Widget build(BuildContext context) {
    final initial = name.isNotEmpty ? name[0].toUpperCase() : '?';

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        boxShadow: const [
          BoxShadow(blurRadius: 12, offset: Offset(0, 4), color: AppColors.shadowAmbient),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  CircleAvatar(
                    radius: 34,
                    backgroundColor: AppColors.primaryContainerLight,
                    child: Text(
                      initial,
                      style: AppTextStyles.headlineLg.copyWith(color: AppColors.primary),
                    ),
                  ),
                  Positioned(
                    right: -2,
                    bottom: -2,
                    child: Container(
                      width: 18,
                      height: 18,
                      decoration: const BoxDecoration(color: AppColors.success, shape: BoxShape.circle),
                      child: const Icon(Icons.check, size: 12, color: Colors.white),
                    ),
                  ),
                ],
              ),
              const Spacer(),
              OutlinedButton.icon(
                onPressed: () => _showEditNameDialog(context, name),
                icon: const Icon(Icons.edit_outlined, size: 15),
                label: const Text('Edit'),
                style: OutlinedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  shape: const StadiumBorder(),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(name, style: AppTextStyles.headlineMd),
          const SizedBox(height: 4),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerLow,
              borderRadius: BorderRadius.circular(999),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.verified, size: 13, color: AppColors.success),
                const SizedBox(width: 4),
                Text(email, style: AppTextStyles.labelSm),
              ],
            ),
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              const Icon(Icons.explore_outlined, size: 13, color: AppColors.textMuted),
              const SizedBox(width: 4),
              Text('Triply Explorer • Member since 2026', style: AppTextStyles.bodySm),
            ],
          ),
          const SizedBox(height: 14),
          const Divider(height: 1),
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                child: _StatBlock(
                  value: '${stats?.totalTrips ?? 0}',
                  label: 'Trips',
                ),
              ),
              Expanded(
                child: _StatBlock(
                  value: '${stats?.totalSavedPlaces ?? 0}',
                  label: 'Saved',
                ),
              ),
              Expanded(
                child: _StatBlock(
                  value: '${stats?.totalCountries ?? 0}',
                  label: 'Countries',
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  void _showEditNameDialog(BuildContext context, String currentName) {
    final controller = TextEditingController(text: currentName);

    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('Edit display name'),
        content: TextField(
          controller: controller,
          autofocus: true,
          decoration: const InputDecoration(isDense: true, border: OutlineInputBorder()),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            onPressed: () async {
              final auth = context.read<AuthProvider>();
              final succeeded = await auth.updateDisplayName(controller.text);
              if (!dialogContext.mounted) return;
              Navigator.of(dialogContext).pop();
              if (!succeeded) {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text(
                      auth.errorMessage ?? 'Unable to save your name.',
                    ),
                  ),
                );
              }
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
  }
}

class _StatBlock extends StatelessWidget {
  const _StatBlock({required this.value, required this.label});

  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(value, style: AppTextStyles.headlineMd.copyWith(color: AppColors.primary)),
        const SizedBox(height: 2),
        Text(label, style: AppTextStyles.labelSm),
      ],
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return Text(
      label,
      style: AppTextStyles.labelSm.copyWith(fontWeight: FontWeight.w700, letterSpacing: 0.6),
    );
  }
}

class _PreferencesCard extends StatelessWidget {
  const _PreferencesCard({required this.settings});

  final UserSettingsProvider settings;

  @override
  Widget build(BuildContext context) {
    final preferences = settings.preferences;
    final currencyLabel = preferences?.preferredCurrency ?? '—';
    final useKm = preferences?.distanceUnit != 'MILES';

    return _SettingsCard(
      children: [
        _SettingsRow(
          icon: Icons.attach_money,
          title: 'Preferred Currency',
          subtitle: 'Auto-converts all daily budgets',
          trailing: _Dropdown(
            label: currencyLabel,
            onTap: preferences == null ? null : () => _pickCurrency(context),
          ),
        ),
        _SettingsRow(
          icon: Icons.directions_walk,
          title: 'Distance & Walking Units',
          trailing: _SegmentedToggle(
            leftLabel: 'Kilometers (km)',
            rightLabel: 'Miles (mi)',
            leftSelected: useKm,
            onChanged: preferences == null
                ? (_) {}
                : (value) => settings.setDistanceUnit(value),
          ),
          stackTrailing: true,
        ),
        _SettingsRow(
          icon: Icons.tune,
          title: 'Default Pacing',
          subtitle: 'Itinerary stops per day',
          trailing: _Dropdown(
            label: preferences?.pacingLabel ?? '—',
            onTap: preferences == null ? null : () => _pickPacing(context),
          ),
        ),
      ],
    );
  }

  Future<void> _pickCurrency(BuildContext context) async {
    List<CurrencyOption> currencies;
    try {
      currencies = await settings.getCurrencies();
    } catch (_) {
      if (context.mounted) _showMessage(context, 'Unable to load currencies.');
      return;
    }
    if (!context.mounted) return;

    final selected = await _showOptionsSheet<CurrencyOption>(
      context,
      title: 'Preferred Currency',
      options: [
        for (final currency in currencies)
          (currency, '${currency.isoCode}  ${currency.symbol}'),
      ],
      isSelected: (currency) =>
          currency.id == settings.preferences?.preferredCurrencyId,
    );
    if (selected == null) return;

    await settings.setCurrency(selected);
    if (context.mounted) _showSaveError(context);
  }

  Future<void> _pickPacing(BuildContext context) async {
    final selected = await _showOptionsSheet<String>(
      context,
      title: 'Default Pacing',
      options: const [
        ('RELAXED', 'Relaxed (2-3 stops)'),
        ('BALANCED', 'Balanced (4-5 stops)'),
        ('FAST', 'Fast-Paced (6+ stops)'),
      ],
      isSelected: (pacing) => pacing == settings.preferences?.pacing,
    );
    if (selected == null) return;

    await settings.setPacing(selected);
    if (context.mounted) _showSaveError(context);
  }

  void _showSaveError(BuildContext context) {
    final error = settings.errorMessage;
    if (error != null) _showMessage(context, error);
  }
}

void _showMessage(BuildContext context, String message) {
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
  );
}

Future<T?> _showOptionsSheet<T>(
  BuildContext context, {
  required String title,
  required List<(T, String)> options,
  required bool Function(T) isSelected,
}) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (context) => SafeArea(
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.of(context).size.height * 0.6,
        ),
        child: ListView(
          shrinkWrap: true,
          padding: const EdgeInsets.fromLTRB(8, 16, 8, 8),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
              child: Text(title, style: AppTextStyles.headlineSm),
            ),
            for (final (value, label) in options)
              ListTile(
                title: Text(label, style: AppTextStyles.labelLg),
                trailing: isSelected(value)
                    ? const Icon(Icons.check, color: AppColors.primary)
                    : null,
                onTap: () => Navigator.of(context).pop(value),
              ),
          ],
        ),
      ),
    ),
  );
}

class _PrivacyCard extends StatefulWidget {
  const _PrivacyCard();

  @override
  State<_PrivacyCard> createState() => _PrivacyCardState();
}

/// No backend field for these yet, so they're remembered on-device only.
class _PrivacyCardState extends State<_PrivacyCard> {
  static const _smartBudgetKey = 'pref_cost_estimates';
  static const _offlineSyncKey = 'pref_offline_sync';
  static const _storage = FlutterSecureStorage();

  bool _smartBudget = true;
  bool _offlineSync = true;

  @override
  void initState() {
    super.initState();
    _restore();
  }

  Future<void> _restore() async {
    try {
      final smartBudget = await _storage.read(key: _smartBudgetKey);
      final offlineSync = await _storage.read(key: _offlineSyncKey);
      if (!mounted) return;
      setState(() {
        _smartBudget = smartBudget != 'false';
        _offlineSync = offlineSync != 'false';
      });
    } catch (_) {
      // Keep the defaults if storage is unavailable.
    }
  }

  void _set(String key, bool value, void Function(bool) apply) {
    setState(() => apply(value));
    _storage.write(key: key, value: '$value').ignore();
  }

  @override
  Widget build(BuildContext context) {
    return _SettingsCard(
      children: [
        _SettingsRow(
          icon: Icons.savings_outlined,
          title: 'Trip Cost Estimates',
          subtitle: 'Costs are calculated from curated place prices.',
          trailing: Switch(
            value: _smartBudget,
            activeThumbColor: AppColors.primary,
            onChanged: (value) =>
                _set(_smartBudgetKey, value, (v) => _smartBudget = v),
          ),
        ),
        _SettingsRow(
          icon: Icons.cloud_sync_outlined,
          title: 'Offline Sync & Caching',
          subtitle: 'Pre-download maps and vouchers',
          trailing: Switch(
            value: _offlineSync,
            activeThumbColor: AppColors.primary,
            onChanged: (value) =>
                _set(_offlineSyncKey, value, (v) => _offlineSync = v),
          ),
        ),
      ],
    );
  }
}

class _AboutCard extends StatelessWidget {
  const _AboutCard();

  @override
  Widget build(BuildContext context) {
    return _SettingsCard(
      children: [
        _LinkRow(
          icon: Icons.description_outlined,
          title: 'Terms of Service',
          builder: (_) => const TermsOfServiceScreen(),
        ),
        _LinkRow(
          icon: Icons.privacy_tip_outlined,
          title: 'Privacy Policy & GDPR',
          builder: (_) => const PrivacyPolicyScreen(),
        ),
        _LinkRow(
          icon: Icons.map_outlined,
          title: 'Supported Destinations Directory',
          builder: (_) => const SupportedDestinationsScreen(),
        ),
      ],
    );
  }
}

class _SettingsCard extends StatelessWidget {
  const _SettingsCard({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        boxShadow: const [
          BoxShadow(blurRadius: 10, offset: Offset(0, 3), color: AppColors.shadowAmbient),
        ],
      ),
      child: Column(
        children: [
          for (var i = 0; i < children.length; i++) ...[
            if (i > 0) const Divider(height: 1, indent: 16, endIndent: 16),
            children[i],
          ],
        ],
      ),
    );
  }
}

class _SettingsRow extends StatelessWidget {
  const _SettingsRow({
    required this.icon,
    required this.title,
    this.subtitle,
    required this.trailing,
    this.stackTrailing = false,
  });

  final IconData icon;
  final String title;
  final String? subtitle;
  final Widget trailing;
  final bool stackTrailing;

  @override
  Widget build(BuildContext context) {
    final leading = Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: 34,
          height: 34,
          decoration: const BoxDecoration(color: AppColors.surfaceContainerLow, shape: BoxShape.circle),
          child: Icon(icon, size: 16, color: AppColors.secondary),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: AppTextStyles.labelLg),
              if (subtitle != null) Text(subtitle!, style: AppTextStyles.bodySm),
            ],
          ),
        ),
      ],
    );

    return Padding(
      padding: const EdgeInsets.all(14),
      child: stackTrailing
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                leading,
                const SizedBox(height: 10),
                trailing,
              ],
            )
          : Row(
              children: [
                Expanded(child: leading),
                trailing,
              ],
            ),
    );
  }
}

class _LinkRow extends StatelessWidget {
  const _LinkRow({
    required this.icon,
    required this.title,
    required this.builder,
  });

  final IconData icon;
  final String title;
  final WidgetBuilder builder;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () =>
          Navigator.of(context).push(MaterialPageRoute<void>(builder: builder)),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Icon(icon, size: 18, color: AppColors.secondary),
            const SizedBox(width: 10),
            Expanded(child: Text(title, style: AppTextStyles.labelLg)),
            const Icon(Icons.chevron_right, color: AppColors.textMuted),
          ],
        ),
      ),
    );
  }
}

class _Dropdown extends StatelessWidget {
  const _Dropdown({required this.label, this.onTap});

  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(label, style: AppTextStyles.labelMd),
          const SizedBox(width: 4),
          const Icon(Icons.keyboard_arrow_down, size: 16, color: AppColors.textMuted),
        ],
      ),
      ),
    );
  }
}

class _SegmentedToggle extends StatelessWidget {
  const _SegmentedToggle({
    required this.leftLabel,
    required this.rightLabel,
    required this.leftSelected,
    required this.onChanged,
  });

  final String leftLabel;
  final String rightLabel;
  final bool leftSelected;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        children: [
          Expanded(child: _segment(leftLabel, leftSelected, () => onChanged(true))),
          Expanded(child: _segment(rightLabel, !leftSelected, () => onChanged(false))),
        ],
      ),
    );
  }

  Widget _segment(String label, bool selected, VoidCallback onTap) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 8),
        decoration: BoxDecoration(
          color: selected ? AppColors.primary : Colors.transparent,
          borderRadius: BorderRadius.circular(999),
        ),
        alignment: Alignment.center,
        child: Text(
          label,
          style: AppTextStyles.labelSm.copyWith(
            color: selected ? Colors.white : AppColors.onSurface,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
    );
  }
}

class _LogOutButton extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      child: OutlinedButton.icon(
        onPressed: () async {
          await context.read<AuthProvider>().logout();
          if (context.mounted) {
            Navigator.of(context).pushNamedAndRemoveUntil('/login', (route) => false);
          }
        },
        icon: const Icon(Icons.logout, size: 17, color: AppColors.error),
        label: const Text('Log Out', style: TextStyle(color: AppColors.error, fontWeight: FontWeight.w700)),
        style: OutlinedButton.styleFrom(
          backgroundColor: AppColors.errorBg,
          side: BorderSide.none,
          padding: const EdgeInsets.symmetric(vertical: 16),
          shape: const StadiumBorder(),
        ),
      ),
    );
  }
}
