import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../core/validation/trip_validators.dart';
import '../../providers/trip_creation_provider.dart';
import '../../widgets/app_scaffold.dart';
import '../../widgets/primary_button.dart';

class TripDetailsScreen extends StatefulWidget {
  const TripDetailsScreen({super.key});

  @override
  State<TripDetailsScreen> createState() => _TripDetailsScreenState();
}

class _TripDetailsScreenState extends State<TripDetailsScreen> {
  late DateTime _startDate;
  late DateTime _endDate;
  late int _travelers;
  double? _budget;

  @override
  void initState() {
    super.initState();

    final data = context.read<TripCreationProvider>().data;

    final today = DateTime.now();

    _startDate =
        data.startDate ?? DateTime(today.year, today.month, today.day + 14);

    _endDate =
        data.endDate ?? _startDate.add(const Duration(days: 7));

    _travelers = data.travelers;
    _budget = data.budget;
  }

  Future<void> _selectDateRange() async {
    final provider = context.read<TripCreationProvider>();

    final initialStart = _startDate;
    final initialEnd = _endDate;

    final picked = await showDateRangePicker(
      context: context,
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 730)),
      initialDateRange: DateTimeRange(
        start: initialStart,
        end: initialEnd,
      ),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: Theme.of(context).colorScheme.copyWith(
              primary: AppColors.primary,
              onPrimary: AppColors.onPrimary,
              surface: AppColors.surfaceContainerLowest,
            ),
          ),
          child: child!,
        );
      },
    );

    if (picked == null) return;

    setState(() {
      _startDate = picked.start;
      _endDate = picked.end;
    });

    provider.setDates(
      startDate: picked.start,
      endDate: picked.end,
    );
  }

  void _decreaseTravelers() {
    if (_travelers <= 1) return;

    setState(() {
      _travelers--;
    });

    context.read<TripCreationProvider>().setTravelers(_travelers);
  }

  void _increaseTravelers() {
    if (_travelers >= 20) return;

    setState(() {
      _travelers++;
    });

    context.read<TripCreationProvider>().setTravelers(_travelers);
  }

  Future<void> _editBudget() async {
    final controller = TextEditingController(
      text: _budget?.toStringAsFixed(0) ?? '',
    );
    String? errorText;

    final result = await showDialog<double>(
      context: context,
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setState) {
            return AlertDialog(
              title: const Text('Target Budget'),
              content: TextField(
                controller: controller,
                autofocus: true,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: InputDecoration(
                  prefixText: '\$ ',
                  hintText: '2500',
                  filled: true,
                  fillColor: AppColors.surfaceContainerLow,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(14),
                    borderSide: BorderSide.none,
                  ),
                  errorText: errorText,
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(context),
                  child: const Text('Cancel'),
                ),
                ElevatedButton(
                  onPressed: () {
                    final error = TripValidators.budgetInput(
                      controller.text,
                      required: true,
                    );

                    if (error != null) {
                      setState(() => errorText = error);
                      return;
                    }

                    Navigator.pop(context, double.parse(controller.text.trim()));
                  },
                  child: const Text('Save'),
                ),
              ],
            );
          },
        );
      },
    );

    controller.dispose();

    if (result == null) return;

    setState(() {
      _budget = result;
    });

    context.read<TripCreationProvider>().setBudget(result);
  }

  void _continue(TripCreationProvider provider) {
    final travelerError = TripValidators.travelerCount(_travelers);
    if (travelerError != null) {
      _showError(travelerError);
      return;
    }

    final dateError = TripValidators.dateRange(_startDate, _endDate);
    if (dateError != null) {
      _showError(dateError);
      return;
    }

    provider.setDates(startDate: _startDate, endDate: _endDate);
    provider.setTravelers(_travelers);

    if (_budget != null) {
      provider.setBudget(_budget!);
    }

    provider.next();
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  String _formatDate(DateTime date) {
    const months = [
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ];

    return '${months[date.month - 1]} ${date.day}, ${date.year}';
  }

  String _shortDate(DateTime date) {
    const months = [
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'May',
      'Jun',
      'Jul',
      'Aug',
      'Sep',
      'Oct',
      'Nov',
      'Dec',
    ];

    return '${months[date.month - 1]} ${date.day}, ${date.year}';
  }

  int _tripNights() {
    final difference = _endDate.difference(_startDate).inDays;
    return difference < 1 ? 1 : difference;
  }

  String _destinationTitle(TripCreationProvider provider) {
    final destination = provider.data.destination;

    if (destination == null || destination.isEmpty) {
      return 'Your Trip';
    }

    return destination;
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();

    return AppScaffold(
      body: Column(
        children: [
          _buildHeader(provider),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _buildTripSummary(provider),
                  const SizedBox(height: 8),
                  Text(
                    'Set your journey parameters',
                    style: AppTextStyles.headlineSm.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Dates and group size determine daily pacing '
                        'and accurate cost estimates.',
                    style: AppTextStyles.bodySm.copyWith(
                      color: AppColors.textMuted,
                    ),
                  ),
                  const SizedBox(height: 14),

                  _buildTravelWindowCard(),

                  const SizedBox(height: 12),

                  _buildTravelersCard(),

                  const SizedBox(height: 12),

                  _buildBudgetCard(),

                  const SizedBox(height: 12),

                  _buildFestivalCard(),

                  const SizedBox(height: 18),

                  PrimaryButton(
                    label: 'Continue to Interests',
                    fullWidth: true,
                    onPressed: () => _continue(provider),
                  ),

                  const SizedBox(height: 8),

                  Center(
                    child: Text(
                      'You can customize your schedule later anytime',
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.textMuted,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildHeader(TripCreationProvider provider) {
    return Column(
      children: [
        Container(
          height: 52,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: BoxDecoration(
            color: AppColors.surface,
            border: Border(
              bottom: BorderSide(
                color: AppColors.surfaceContainer,
              ),
            ),
          ),
          child: Row(
            children: [
              IconButton(
                onPressed: provider.back,
                icon: const Icon(Icons.arrow_back_ios_new_rounded),
                iconSize: 18,
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(
                  minWidth: 32,
                  minHeight: 32,
                ),
              ),
              const SizedBox(width: 4),
              Text(
                'Triply',
                style: AppTextStyles.labelLg.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
              const Spacer(),
              const Icon(
                Icons.notifications_none_rounded,
                size: 21,
              ),
              const SizedBox(width: 12),
              Container(
                width: 28,
                height: 28,
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.person,
                  color: AppColors.onPrimary,
                  size: 15,
                ),
              ),
            ],
          ),
        ),

        Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
          child: Row(
            children: [
              Text(
                'Step 2 of 5',
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.secondary,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const Spacer(),
              Text(
                '${((_travelers).clamp(1, 20))} travelers',
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.textMuted,
                ),
              ),
            ],
          ),
        ),

        Padding(
          padding: const EdgeInsets.fromLTRB(16, 6, 16, 4),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(10),
            child: LinearProgressIndicator(
              value: 0.40,
              minHeight: 4,
              backgroundColor: AppColors.surfaceContainer,
              valueColor: const AlwaysStoppedAnimation<Color>(
                AppColors.primary,
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildTripSummary(TripCreationProvider provider) {
    return Container(
      margin: const EdgeInsets.only(bottom: 2),
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 7,
      ),
      decoration: BoxDecoration(
        color: AppColors.primaryContainerLight.withOpacity(0.45),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        children: [
          const Icon(
            Icons.location_on_rounded,
            color: AppColors.primary,
            size: 15,
          ),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              _destinationTitle(provider),
              style: AppTextStyles.labelSm.copyWith(
                color: AppColors.primary,
                fontWeight: FontWeight.w600,
              ),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          Text(
            'AI Suggestions',
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.secondary,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTravelWindowCard() {
    return _SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _IconContainer(
                icon: Icons.calendar_month_rounded,
              ),
              const SizedBox(width: 8),
              Text(
                'Travel Window',
                style: AppTextStyles.labelLg.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
              const Spacer(),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 8,
                  vertical: 4,
                ),
                decoration: BoxDecoration(
                  color: AppColors.primaryContainerLight,
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  '${_tripNights()} Days / ${_tripNights()} Nights',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 12),

          Row(
            children: [
              Expanded(
                child: _DateBox(
                  label: 'Start Date',
                  date: _shortDate(_startDate),
                  onTap: _selectDateRange,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: _DateBox(
                  label: 'End Date',
                  date: _shortDate(_endDate),
                  onTap: _selectDateRange,
                ),
              ),
            ],
          ),

          const SizedBox(height: 12),

          _MiniCalendar(
            selectedStart: _startDate,
            selectedEnd: _endDate,
            onTap: _selectDateRange,
          ),
        ],
      ),
    );
  }

  Widget _buildTravelersCard() {
    return _SectionCard(
      child: Row(
        children: [
          _IconContainer(
            icon: Icons.groups_rounded,
          ),
          const SizedBox(width: 8),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Travelers',
                  style: AppTextStyles.labelLg.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  'Adults traveling together',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ),
          ),

          Container(
            height: 34,
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerLow,
              borderRadius: BorderRadius.circular(18),
            ),
            child: Row(
              children: [
                IconButton(
                  onPressed: _decreaseTravelers,
                  icon: const Icon(Icons.remove_rounded),
                  iconSize: 15,
                  padding: EdgeInsets.zero,
                  constraints: const BoxConstraints(
                    minWidth: 32,
                  ),
                ),
                Text(
                  '$_travelers',
                  style: AppTextStyles.labelLg.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                IconButton(
                  onPressed: _increaseTravelers,
                  icon: const Icon(Icons.add_rounded),
                  iconSize: 15,
                  padding: EdgeInsets.zero,
                  constraints: const BoxConstraints(
                    minWidth: 32,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBudgetCard() {
    return _SectionCard(
      child: InkWell(
        onTap: _editBudget,
        borderRadius: BorderRadius.circular(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                _IconContainer(
                  icon: Icons.account_balance_wallet_outlined,
                ),
                const SizedBox(width: 8),
                Text(
                  'Target Budget',
                  style: AppTextStyles.labelLg.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(width: 8),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 7,
                    vertical: 3,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.surfaceContainerLow,
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Text(
                    'Optional',
                    style: AppTextStyles.labelSm.copyWith(
                      color: AppColors.secondary,
                    ),
                  ),
                ),
              ],
            ),

            const SizedBox(height: 10),

            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(
                horizontal: 12,
                vertical: 11,
              ),
              decoration: BoxDecoration(
                color: AppColors.surfaceContainerLow,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(
                children: [
                  Text(
                    'USD (\$)',
                    style: AppTextStyles.labelSm.copyWith(
                      color: AppColors.secondary,
                    ),
                  ),
                  const SizedBox(width: 16),
                  Text(
                    _budget == null
                        ? 'Set budget'
                        : '\$${_budget!.toStringAsFixed(0)}',
                    style: AppTextStyles.labelLg.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const Spacer(),
                  const Icon(
                    Icons.tune_rounded,
                    size: 17,
                    color: AppColors.secondary,
                  ),
                ],
              ),
            ),

            const SizedBox(height: 7),

            Text(
              'Helps AI balance dining, transit, and '
                  'accommodation levels for $_travelers travelers.',
              style: AppTextStyles.labelSm.copyWith(
                color: AppColors.textMuted,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFestivalCard() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.primaryContainerLight.withOpacity(0.65),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 32,
            height: 32,
            decoration: const BoxDecoration(
              color: AppColors.primary,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.event_rounded,
              color: AppColors.onPrimary,
              size: 17,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Mid-Autumn Festival Peak',
                  style: AppTextStyles.labelMd.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  'Mild weather, vibrant autumn foliage expected.',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.secondary,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({
    required this.child,
  });

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLowest,
        borderRadius: BorderRadius.circular(18),
        boxShadow: [
          BoxShadow(
            color: AppColors.shadowAmbient,
            blurRadius: 12,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: child,
    );
  }
}

class _IconContainer extends StatelessWidget {
  const _IconContainer({
    required this.icon,
  });

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 30,
      height: 30,
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(9),
      ),
      child: Icon(
        icon,
        size: 16,
        color: AppColors.secondary,
      ),
    );
  }
}

class _DateBox extends StatelessWidget {
  const _DateBox({
    required this.label,
    required this.date,
    required this.onTap,
  });

  final String label;
  final String date;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.all(9),
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLow,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: AppTextStyles.labelSm.copyWith(
                color: AppColors.textMuted,
              ),
            ),
            const SizedBox(height: 3),
            Text(
              date,
              style: AppTextStyles.labelMd.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MiniCalendar extends StatelessWidget {
  const _MiniCalendar({
    required this.selectedStart,
    required this.selectedEnd,
    required this.onTap,
  });

  final DateTime selectedStart;
  final DateTime selectedEnd;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final monthStart = DateTime(
      selectedStart.year,
      selectedStart.month,
      1,
    );

    final daysInMonth = DateTime(
      selectedStart.year,
      selectedStart.month + 1,
      0,
    ).day;

    final firstWeekday = monthStart.weekday % 7;

    const weekdays = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

    return Column(
      children: [
        Row(
          children: [
            Text(
              '${_monthName(selectedStart.month)} ${selectedStart.year}',
              style: AppTextStyles.labelSm.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const Spacer(),
            IconButton(
              onPressed: onTap,
              icon: const Icon(Icons.chevron_left_rounded),
              iconSize: 18,
              constraints: const BoxConstraints(
                minWidth: 28,
                minHeight: 28,
              ),
              padding: EdgeInsets.zero,
            ),
            IconButton(
              onPressed: onTap,
              icon: const Icon(Icons.chevron_right_rounded),
              iconSize: 18,
              constraints: const BoxConstraints(
                minWidth: 28,
                minHeight: 28,
              ),
              padding: EdgeInsets.zero,
            ),
          ],
        ),

        Row(
          children: weekdays.map((day) {
            return Expanded(
              child: Center(
                child: Text(
                  day,
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.textMuted,
                  ),
                ),
              ),
            );
          }).toList(),
        ),

        const SizedBox(height: 4),

        GridView.builder(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          itemCount: firstWeekday + daysInMonth,
          gridDelegate:
          const SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: 7,
            mainAxisSpacing: 5,
            crossAxisSpacing: 2,
          ),
          itemBuilder: (context, index) {
            if (index < firstWeekday) {
              return const SizedBox();
            }

            final day = index - firstWeekday + 1;

            final date = DateTime(
              selectedStart.year,
              selectedStart.month,
              day,
            );

            final isStart = _sameDay(date, selectedStart);
            final isEnd = _sameDay(date, selectedEnd);

            final inRange =
                date.isAfter(selectedStart.subtract(
                  const Duration(days: 1),
                )) &&
                    date.isBefore(selectedEnd.add(
                      const Duration(days: 1),
                    ));

            return GestureDetector(
              onTap: onTap,
              child: Container(
                decoration: BoxDecoration(
                  color: inRange
                      ? AppColors.primaryContainerLight
                      : Colors.transparent,
                  shape: BoxShape.circle,
                ),
                child: Center(
                  child: Container(
                    width: 25,
                    height: 25,
                    decoration: BoxDecoration(
                      color: isStart || isEnd
                          ? AppColors.primary
                          : Colors.transparent,
                      shape: BoxShape.circle,
                    ),
                    child: Center(
                      child: Text(
                        '$day',
                        style: AppTextStyles.labelSm.copyWith(
                          color: isStart || isEnd
                              ? AppColors.onPrimary
                              : AppColors.onSurface,
                          fontWeight: isStart || isEnd
                              ? FontWeight.w700
                              : FontWeight.w500,
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            );
          },
        ),
      ],
    );
  }

  bool _sameDay(DateTime a, DateTime b) {
    return a.year == b.year &&
        a.month == b.month &&
        a.day == b.day;
  }

  String _monthName(int month) {
    const months = [
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ];

    return months[month - 1];
  }
}