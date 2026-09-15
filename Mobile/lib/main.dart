import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/network/api_client.dart';
import 'data/repositories/api_auth_repository.dart';
import 'data/repositories/auth_repository.dart';
import 'presentation/providers/auth_provider.dart';
import 'presentation/screens/home/home_screen.dart';
import 'presentation/screens/onboarding/onboarding_flow.dart';
import 'presentation/screens/splash_screen.dart';
import 'presentation/widgets/auth/login_screen.dart';
import 'presentation/widgets/auth/register_screen.dart';

void main() {
  final apiClient = ApiClient(
    baseUrl: 'http://10.0.2.2:8080',
  );

  final AuthRepository authRepository = ApiAuthRepository(
    apiClient: apiClient,
  );

  runApp(
    MyApp(
      authRepository: authRepository,
    ),
  );
}

class MyApp extends StatelessWidget {
  const MyApp({
    super.key,
    required this.authRepository,
  });

  final AuthRepository authRepository;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => AuthProvider(
        repository: authRepository,
      ),
      child: MaterialApp(
        title: 'Triply',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          colorScheme: ColorScheme.fromSeed(
            seedColor: const Color(0xFFA83223),
          ),
          scaffoldBackgroundColor: const Color(0xFFFAF8FF),
        ),
        initialRoute: '/splash',
        routes: {
          '/splash': (_) => const SplashScreen(),
          '/onboarding': (_) => const OnboardingFlow(),
          '/login': (_) => const LoginScreen(),
          '/register': (_) => const RegisterScreen(),
          '/home': (_) => const HomeScreen(),
        },
      ),
    );
  }
}