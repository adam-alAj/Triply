import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../providers/auth_provider.dart';
import '../../widgets/app_scaffold.dart';
import '../../widgets/app_text_field.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/auth/auth_header.dart';
import '../../widgets/auth/social_auth_button.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  String? _emailError;
  String? _passwordError;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  bool _validateFields() {
    String? emailError;
    String? passwordError;

    final email = _emailController.text.trim();
    final password = _passwordController.text;

    // Email validation
    if (email.isEmpty) {
      emailError = 'Email is required.';
    } else if (!_isValidEmail(email)) {
      emailError = 'Please enter a valid email address.';
    }

    // Password validation
    if (password.isEmpty) {
      passwordError = 'Password is required.';
    } else if (password.length < 8) {
      passwordError = 'Password must be at least 8 characters.';
    }

    setState(() {
      _emailError = emailError;
      _passwordError = passwordError;
    });

    return emailError == null && passwordError == null;
  }

  bool _isValidEmail(String email) {
    return RegExp(
      r'^[^@\s]+@[^@\s]+\.[^@\s]+$',
    ).hasMatch(email);
  }

  Future<void> _login() async {
    FocusScope.of(context).unfocus();

    if (!_validateFields()) {
      return;
    }

    await context.read<AuthProvider>().login(
      email: _emailController.text.trim(),
      password: _passwordController.text,
    );

    if (!mounted) return;

    final auth = context.read<AuthProvider>();

    if (auth.status == AuthStatus.success) {
      Navigator.pushReplacementNamed(context, '/home');
    }
  }

  @override
  Widget build(BuildContext context) {
    return AppScaffold(
      body: Consumer<AuthProvider>(
        builder: (context, auth, _) {
          return SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                AuthHeader(
                  onBack: () => Navigator.of(context).maybePop(),
                ),

                const SizedBox(height: 12),

                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 14,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.surfaceContainerLow,
                    borderRadius: BorderRadius.circular(28),
                  ),
                  child: Row(
                    children: [
                      Image.asset(
                        'assets/images/Triply Logo.png',
                        width: 32,
                        height: 32,
                      ),

                      const SizedBox(width: 12),

                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Triply AI',
                              style: AppTextStyles.headlineSm,
                            ),
                            Text(
                              'Curated Journeys',
                              style: AppTextStyles.labelSm,
                            ),
                          ],
                        ),
                      ),

                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 6,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.surfaceContainer,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          '🔒 TLS 256-BIT',
                          style: AppTextStyles.labelSm,
                        ),
                      ),
                    ],
                  ),
                ),

                const SizedBox(height: 20),

                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 6,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.primaryContainerLight,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    '✈ Wanderlust Awaits',
                    style: AppTextStyles.labelSm.copyWith(
                      color: AppColors.onPrimaryContainerLight,
                    ),
                  ),
                ),

                const SizedBox(height: 10),

                Text(
                  'Welcome back',
                  style: AppTextStyles.headlineLg,
                ),

                const SizedBox(height: 4),

                Text(
                  'Log in to continue your personalized travel journeys, '
                      'saved itineraries, and smart agent recommendations.',
                  style: AppTextStyles.bodyMd,
                ),

                const SizedBox(height: 24),

                AppTextField(
                  label: 'Email Address',
                  helperText: 'Required',
                  controller: _emailController,
                  leadingIcon: Icons.email_outlined,
                  keyboardType: TextInputType.emailAddress,
                  errorText: _emailError,
                  onChanged: (_) {
                    if (_emailError != null) {
                      setState(() {
                        _emailError = null;
                      });
                    }

                    if (auth.errorMessage != null) {
                      context.read<AuthProvider>().clearError();
                    }
                  },
                ),

                const SizedBox(height: 14),

                AppTextField(
                  label: 'Password',
                  helperText: 'Forgot password?',
                  controller: _passwordController,
                  leadingIcon: Icons.lock_outline,
                  obscureText: true,
                  errorText: _passwordError,
                  onChanged: (_) {
                    if (_passwordError != null) {
                      setState(() {
                        _passwordError = null;
                      });
                    }

                    if (auth.errorMessage != null) {
                      context.read<AuthProvider>().clearError();
                    }
                  },
                ),

                if (auth.errorMessage != null) ...[
                  const SizedBox(height: 10),
                  Text(
                    auth.errorMessage!,
                    style: AppTextStyles.labelSm.copyWith(
                      color: AppColors.formError,
                    ),
                  ),
                ],

                const SizedBox(height: 14),

                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(12),
                  color: AppColors.surfaceContainerLow,
                  child: Row(
                    children: [
                      const Icon(
                        Icons.shield_outlined,
                        size: 18,
                        color: AppColors.formError,
                      ),

                      const SizedBox(width: 8),

                      Expanded(
                        child: Text(
                          'Zero credential leakage. Sessions protected by '
                              'secure, encrypted JWT authentication tokens.',
                          style: AppTextStyles.labelSm,
                        ),
                      ),
                    ],
                  ),
                ),

                const SizedBox(height: 20),

                PrimaryButton(
                  label: 'Log In',
                  icon: Icons.arrow_forward,
                  isLoading: auth.isLoading,
                  onPressed: auth.isLoading ? null : _login,
                ),

                const SizedBox(height: 18),

                Row(
                  children: [
                    const Expanded(
                      child: Divider(),
                    ),
                    Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                      ),
                      child: Text(
                        'OR SIGN IN WITH',
                        style: AppTextStyles.labelSm,
                      ),
                    ),
                    const Expanded(
                      child: Divider(),
                    ),
                  ],
                ),

                const SizedBox(height: 14),

                Row(
                  children: [
                    SocialAuthButton(
                      label: 'Google',
                      icon: const Text(
                        'G',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),

                    const SizedBox(width: 10),

                    SocialAuthButton(
                      label: 'Passkey',
                      icon: const Icon(
                        Icons.fingerprint,
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: 28),

                Center(
                  child: Wrap(
                    alignment: WrapAlignment.center,
                    children: [
                      Text(
                        "Don't have an account? ",
                        style: AppTextStyles.bodyMd,
                      ),

                      GestureDetector(
                        onTap: auth.isLoading
                            ? null
                            : () {
                          Navigator.pushNamed(
                            context,
                            '/register',
                          );
                        },
                        child: Text(
                          'Create account↗',
                          style: AppTextStyles.bodyMd.copyWith(
                            color: AppColors.primary,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),

                const SizedBox(height: 10),

                Center(
                  child: Text(
                    'Privacy Policy   •   Terms of Service',
                    style: AppTextStyles.labelSm,
                  ),
                ),

                const SizedBox(height: 16),
              ],
            ),
          );
        },
      ),
    );
  }
}