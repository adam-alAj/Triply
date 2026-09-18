import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:triply_project/presentation/screens/home/home_screen.dart';
import 'package:triply_project/presentation/screens/my_trips/my_trips_screen.dart';
import 'package:triply_project/presentation/screens/onboarding/onboarding_flow.dart';
import 'package:triply_project/presentation/screens/profile/profile_screen.dart';
import 'package:triply_project/presentation/screens/splash_screen.dart';
import 'package:triply_project/presentation/screens/trip_creation/trip_creation_wizard.dart';
import 'package:triply_project/presentation/screens/trip_overview/trip_overview_screen.dart';
import 'package:triply_project/presentation/widgets/auth/login_screen.dart';
import 'package:triply_project/presentation/widgets/auth/register_screen.dart';

import 'core/network/api_client.dart';
import 'core/network/auth_interceptor.dart';
import 'core/storage/token_storage.dart';
import 'data/repositories/api_auth_repository.dart';
import 'data/repositories/auth_repository.dart';
import 'presentation/providers/auth_provider.dart';

void main() {
  final tokenStorage = TokenStorage();

  // Single shared Dio client for the whole app: every authenticated request
  // (trips, destinations, itinerary, cost) goes through this instance so the
  // Authorization header is attached consistently. See ApiClient's doc
  // comment for how the base URL is picked per platform (Android emulator
  // vs. iOS simulator/desktop).
  final apiClient = ApiClient()
    ..interceptors.add(AuthInterceptor(tokenStorage));

  final AuthRepository authRepository = ApiAuthRepository(
    apiClient: apiClient,
    tokenStorage: tokenStorage,
  );

  runApp(
    MyApp(
      apiClient: apiClient,
      authRepository: authRepository,
    ),
  );
}

class MyApp extends StatelessWidget {
  const MyApp({
    super.key,
    required this.apiClient,
    required this.authRepository,
  });

  final ApiClient apiClient;
  final AuthRepository authRepository;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        // Exposed for repositories added in later steps (trip creation,
        // itinerary, cost) so they don't need their own ApiClient instance.
        Provider<ApiClient>.value(value: apiClient),
        ChangeNotifierProvider(
          create: (_) => AuthProvider(
            repository: authRepository,
          ),
        ),
      ],
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
          '/my-trips': (_) => const MyTripsScreen(),
          '/profile': (_) => const ProfileScreen(),
          '/create-trip': (_) => const TripCreationWizard(),
          // Expects a String tripId passed as the route argument, e.g.
          // Navigator.pushNamed(context, '/trip-overview', arguments: tripId).
          '/trip-overview': (context) => TripOverviewScreen(
                tripId: ModalRoute.of(context)!.settings.arguments as String,
              ),
        },
      ),
    );
  }
}
