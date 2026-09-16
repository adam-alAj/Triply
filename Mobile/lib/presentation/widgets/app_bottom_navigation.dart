import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Which root tab (if any) the current screen represents. A screen pushed
/// on top of a tab (like Trip Overview) uses [none] so it doesn't falsely
/// claim to be one of the four root destinations.
enum AppNavTab { home, myTrips, none, profile }

/// Shared bottom navigation bar — was duplicated per-screen before; now one
/// widget so Home and any screen reachable from it (Trip Overview, etc.)
/// look and behave identically.
class AppBottomNavigation extends StatelessWidget {
  const AppBottomNavigation({super.key, this.selected = AppNavTab.none});

  final AppNavTab selected;

  void _goHome(BuildContext context) {
    if (selected == AppNavTab.home) return;
    Navigator.of(context).pushNamedAndRemoveUntil('/home', (route) => false);
  }

  void _placeholder(BuildContext context, String feature) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('$feature is coming soon.'),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return BottomAppBar(
      color: Colors.white,
      elevation: 10,
      child: SizedBox(
        height: 62,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceAround,
          children: [
            _NavItem(
              icon: Icons.home_outlined,
              label: 'Home',
              selected: selected == AppNavTab.home,
              onTap: () => _goHome(context),
            ),
            _NavItem(
              icon: Icons.luggage_outlined,
              label: 'My Trips',
              selected: selected == AppNavTab.myTrips,
              onTap: () => _placeholder(context, 'My Trips'),
            ),
            _CreateNavButton(
              onTap: () => Navigator.pushNamed(context, '/create-trip'),
            ),
            _NavItem(
              icon: Icons.person_outline,
              label: 'Profile',
              selected: selected == AppNavTab.profile,
              onTap: () => _placeholder(context, 'Profile'),
            ),
          ],
        ),
      ),
    );
  }
}

class _CreateNavButton extends StatefulWidget {
  const _CreateNavButton({required this.onTap});

  final VoidCallback onTap;

  @override
  State<_CreateNavButton> createState() => _CreateNavButtonState();
}

class _CreateNavButtonState extends State<_CreateNavButton> {
  bool _pressed = false;

  void _handleTap() {
    setState(() => _pressed = true);

    Future.delayed(const Duration(milliseconds: 110), () {
      if (!mounted) return;
      setState(() => _pressed = false);
    });

    widget.onTap();
  }

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: _handleTap,
      borderRadius: BorderRadius.circular(16),
      child: SizedBox(
        width: 58,
        height: 62,
        child: Center(
          child: AnimatedScale(
            scale: _pressed ? 0.90 : 1.0,
            duration: const Duration(milliseconds: 110),
            curve: Curves.easeOut,
            child: Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: AppColors.primary,
                borderRadius: BorderRadius.circular(14),
                boxShadow: const [
                  BoxShadow(
                    blurRadius: 8,
                    offset: Offset(0, 3),
                    color: Color(0x22000000),
                  ),
                ],
              ),
              child: const Icon(Icons.add, color: Colors.white, size: 25),
            ),
          ),
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.icon,
    required this.label,
    required this.onTap,
    this.selected = false,
  });

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected ? AppColors.primary : AppColors.secondary;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 20, color: color),
            const SizedBox(height: 2),
            Text(
              label,
              style: AppTextStyles.labelSm.copyWith(
                color: color,
                fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
