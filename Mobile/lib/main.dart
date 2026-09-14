import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:triply_project/presentation/widgets/auth/login_screen.dart';
import 'package:triply_project/presentation/widgets/auth/register_screen.dart';

import 'data/repositories/auth_repository.dart';
import 'data/repositories/mock_auth_repository.dart';
import 'presentation/providers/auth_provider.dart';

void main() {
  final AuthRepository authRepository = MockAuthRepository();

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
        initialRoute: '/login',
        routes: {
          '/login': (_) => const LoginScreen(),
          '/register': (_) => const RegisterScreen(),
        },
      ),
    );
  }
}