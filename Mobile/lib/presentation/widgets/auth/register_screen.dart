import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../providers/auth_provider.dart';
import '../../widgets/app_scaffold.dart';
import '../../widgets/app_text_field.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/auth/auth_header.dart';
import '../../widgets/auth/auth_info_card.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  String? _nameError;
  String? _emailError;
  String? _passwordError;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  bool _validateFields() {
    String? nameError;
    String? emailError;
    String? passwordError;

    final name = _nameController.text.trim();
    final email = _emailController.text.trim();
    final password = _passwordController.text;

    // Full Name validation
    if (name.isEmpty) {
      nameError = 'Full name is required.';
    }

    // Email validation
    if (email.isEmpty) {
      emailError = 'Email is required.';
    } else if (!_isValidEmail(email)) {
      emailError = 'Please enter a valid email address.';
    }

    // Password validation — mirrors the backend's ASP.NET Core Identity
    // policy (min length 8, uppercase, digit, non-alphanumeric) so a form
    // that passes here won't be rejected by the server.
    if (password.isEmpty) {
      passwordError = 'Password is required.';
    } else if (password.length < 8) {
      passwordError = 'Password must be at least 8 characters.';
    } else if (!RegExp(r'[A-Z]').hasMatch(password)) {
      passwordError = 'Password must include an uppercase letter.';
    } else if (!RegExp(r'[0-9]').hasMatch(password)) {
      passwordError = 'Password must include a number.';
    } else if (!RegExp(r'[^a-zA-Z0-9]').hasMatch(password)) {
      passwordError = 'Password must include a special character.';
    }

    setState(() {
      _nameError = nameError;
      _emailError = emailError;
      _passwordError = passwordError;
    });

    return nameError == null &&
        emailError == null &&
        passwordError == null;
  }

  bool _isValidEmail(String email) {
    return RegExp(
      r'^[^@\s]+@[^@\s]+\.[^@\s]+$',
    ).hasMatch(email);
  }

  void _register() {
    FocusScope.of(context).unfocus();

    if (!_validateFields()) {
      return;
    }

    context.read<AuthProvider>().register(
      name: _nameController.text.trim(),
      email: _emailController.text.trim(),
      password: _passwordController.text,
    );
  }

  void _showRegisterSuccess() {
    ScaffoldMessenger.of(context).hideCurrentSnackBar();

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Account created successfully.'),
        duration: Duration(seconds: 2),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AppScaffold(
      body: Consumer<AuthProvider>(
        builder: (context, auth, _) {
          if (auth.status == AuthStatus.success) {
            WidgetsBinding.instance.addPostFrameCallback((_) {
              if (!mounted) return;

              _showRegisterSuccess();
            });
          }

          return SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                AuthHeader(
                  onBack: () => Navigator.of(context).maybePop(),
                ),

                const SizedBox(height: 14),

                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 7,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.surfaceContainerLow,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    '✦ MINDFUL WANDERING',
                    style: AppTextStyles.labelSm,
                  ),
                ),

                const SizedBox(height: 14),

                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 7,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.surfaceContainerLow,
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text(
                        'Step 1 of 2',
                        style: AppTextStyles.labelSm,
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: 14),

                RichText(
                  text: TextSpan(
                    children: [
                      TextSpan(
                        text: 'Create your\n',
                        style: AppTextStyles.headlineLg,
                      ),
                      TextSpan(
                        text: 'account.',
                        style: AppTextStyles.headlineLg.copyWith(
                          color: AppColors.primary,
                        ),
                      ),
                    ],
                  ),
                ),

                const SizedBox(height: 6),

                Text(
                  'Start planning mindful, AI-curated trips in seconds.',
                  style: AppTextStyles.bodyMd,
                ),

                const SizedBox(height: 24),

                AppTextField(
                  label: 'Full Name',
                  controller: _nameController,
                  leadingIcon: Icons.person_outline,
                  errorText: _nameError,
                  onChanged: (_) {
                    if (_nameError != null) {
                      setState(() {
                        _nameError = null;
                      });
                    }

                    if (auth.errorMessage != null) {
                      context.read<AuthProvider>().clearError();
                    }
                  },
                ),

                const SizedBox(height: 14),

                AppTextField(
                  label: 'Email Address',
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
                  helperText: 'Min. 8 characters, 1 uppercase, 1 number, 1 symbol',
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

                const SizedBox(height: 20),

                AuthInfoCard(
                  text: 'Includes tailored itinerary syncing, real-time pace '
                      'balancing, and intuitive offline trip views.',
                ),

                const SizedBox(height: 18),

                Center(
                  child: Text(
                    "By registering, you agree to Triply's Terms and Privacy Policy.",
                    textAlign: TextAlign.center,
                    style: AppTextStyles.bodySm,
                  ),
                ),

                const SizedBox(height: 20),

                PrimaryButton(
                  label: 'Create Account',
                  icon: Icons.arrow_forward,
                  isLoading: auth.isLoading,
                  onPressed: auth.isLoading ? null : _register,
                ),

                if (auth.errorMessage != null) ...[
                  const SizedBox(height: 10),
                  Center(
                    child: Text(
                      auth.errorMessage!,
                      textAlign: TextAlign.center,
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.formError,
                      ),
                    ),
                  ),
                ],

                const SizedBox(height: 14),

                Center(
                  child: Text(
                    '🛡 End-to-End Encrypted   •   ◉ Private Itineraries',
                    style: AppTextStyles.labelSm,
                  ),
                ),

                const SizedBox(height: 24),

                Center(
                  child: Wrap(
                    alignment: WrapAlignment.center,
                    children: [
                      Text(
                        'Already have an account? ',
                        style: AppTextStyles.bodyMd,
                      ),
                      GestureDetector(
                        onTap: auth.isLoading
                            ? null
                            : () {
                          Navigator.pushReplacementNamed(
                            context,
                            '/login',
                          );
                        },
                        child: Text(
                          'Log In',
                          style: AppTextStyles.bodyMd.copyWith(
                            color: AppColors.primary,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
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