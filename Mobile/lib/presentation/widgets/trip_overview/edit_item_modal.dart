import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_overview_data.dart';
import '../primary_button.dart';

/// Edit Item Modal (UI Pages §6). Returns the edited item, or null if the
/// user cancelled. Saving always marks the item user-modified — that's the
/// whole point of this surface per the task's acceptance criteria.
///
/// [dayLabel] is the "Day 2 • Gion District" context line, [position] and
/// [totalItems] drive the "Position X of N" sequence pill — both computed
/// by the caller from the day's ordered item list. Move Earlier/Later only
/// adjust this item's own `orderIndex` locally (PENDING BACKEND: there is
/// no cross-item reorder endpoint yet, same gap noted on `removeItem`).
Future<ItineraryItemData?> showEditItemModal(
  BuildContext context,
  ItineraryItemData item, {
  required String dayLabel,
  required int position,
  required int totalItems,
}) {
  return showModalBottomSheet<ItineraryItemData>(
    context: context,
    isScrollControlled: true,
    backgroundColor: AppColors.surfaceContainerLowest,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (_) => _EditItemModal(
      item: item,
      dayLabel: dayLabel,
      position: position,
      totalItems: totalItems,
    ),
  );
}

class _EditItemModal extends StatefulWidget {
  const _EditItemModal({
    required this.item,
    required this.dayLabel,
    required this.position,
    required this.totalItems,
  });

  final ItineraryItemData item;
  final String dayLabel;
  final int position;
  final int totalItems;

  @override
  State<_EditItemModal> createState() => _EditItemModalState();
}

class _EditItemModalState extends State<_EditItemModal> {
  late final _nameController = TextEditingController(text: widget.item.placeName);
  late final _notesController = TextEditingController(text: widget.item.notes ?? '');

  late int _startTimeMinutes = widget.item.startTimeMinutes ?? 9 * 60;
  late int _durationMinutes = widget.item.durationMinutes ?? 60;
  late int _position = widget.position;

  static const _timeStepMinutes = 15;
  static const _durationStepMinutes = 15;

  @override
  void dispose() {
    _nameController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  void _save() {
    final name = _nameController.text.trim();
    Navigator.of(context).pop(
      widget.item.copyWith(
        placeName: name.isEmpty ? widget.item.placeName : name,
        notes: _notesController.text.trim(),
        startTimeMinutes: _startTimeMinutes,
        durationMinutes: _durationMinutes,
        orderIndex: _position,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          top: 16,
          bottom: MediaQuery.of(context).viewInsets.bottom + 20,
        ),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    width: 8,
                    height: 8,
                    margin: const EdgeInsets.only(right: 8),
                    decoration: const BoxDecoration(
                      color: AppColors.primary,
                      shape: BoxShape.circle,
                    ),
                  ),
                  Expanded(
                    child: Text('Edit Itinerary Item', style: AppTextStyles.headlineSm),
                  ),
                  InkWell(
                    onTap: () => Navigator.of(context).pop(),
                    customBorder: const CircleBorder(),
                    child: Container(
                      width: 32,
                      height: 32,
                      decoration: const BoxDecoration(
                        color: AppColors.surfaceContainer,
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(Icons.close, size: 16, color: AppColors.secondary),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 4),
              Padding(
                padding: const EdgeInsets.only(left: 16),
                child: Text(
                  'Custom adjustments for ${widget.dayLabel}',
                  style: AppTextStyles.bodySm,
                ),
              ),
              const SizedBox(height: 20),
              _FieldLabel(icon: Icons.restaurant_menu, label: 'Activity Name'),
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
                decoration: BoxDecoration(
                  color: AppColors.surfaceContainerLow,
                  borderRadius: BorderRadius.circular(9999),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _nameController,
                        style: AppTextStyles.labelLg,
                        decoration: const InputDecoration(
                          isDense: true,
                          border: InputBorder.none,
                        ),
                      ),
                    ),
                    const Icon(Icons.edit_outlined, size: 16, color: AppColors.textMuted),
                  ],
                ),
              ),
              const SizedBox(height: 18),
              _FieldLabel(icon: Icons.schedule, label: 'Scheduled Start Time'),
              const SizedBox(height: 8),
              _StepperField(
                valueLabel: formatClockTime(_startTimeMinutes),
                onDecrement: () => setState(
                  () => _startTimeMinutes -= _timeStepMinutes,
                ),
                onIncrement: () => setState(
                  () => _startTimeMinutes += _timeStepMinutes,
                ),
              ),
              const SizedBox(height: 18),
              _FieldLabel(icon: Icons.hourglass_bottom, label: 'Allocated Time'),
              const SizedBox(height: 8),
              _StepperField(
                valueLabel: formatDurationLabel(_durationMinutes),
                onDecrement: _durationMinutes > _durationStepMinutes
                    ? () => setState(() => _durationMinutes -= _durationStepMinutes)
                    : null,
                onIncrement: () => setState(
                  () => _durationMinutes += _durationStepMinutes,
                ),
              ),
              const SizedBox(height: 18),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const _FieldLabel(icon: Icons.swap_vert, label: 'Itinerary Sequence'),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.warningBg,
                      borderRadius: BorderRadius.circular(9999),
                    ),
                    child: Text(
                      'Position $_position of ${widget.totalItems}',
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.warning,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _position > 1
                          ? () => setState(() => _position -= 1)
                          : null,
                      icon: const Icon(Icons.arrow_upward, size: 16),
                      label: const Text('Move Earlier'),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _position < widget.totalItems
                          ? () => setState(() => _position += 1)
                          : null,
                      icon: const Icon(Icons.arrow_downward, size: 16),
                      label: const Text('Move Later'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 18),
              _FieldLabel(icon: Icons.edit_note, label: 'Personal Notes & Reservations'),
              const SizedBox(height: 8),
              TextField(
                controller: _notesController,
                maxLines: 3,
                decoration: InputDecoration(
                  isDense: true,
                  filled: true,
                  fillColor: AppColors.surfaceContainerLow,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(16),
                    borderSide: BorderSide.none,
                  ),
                  hintText: 'Add a note or reservation reference...',
                ),
              ),
              const SizedBox(height: 22),
              Row(
                children: [
                  Expanded(
                    child: TextButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: const Text('Cancel'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: PrimaryButton(label: 'Save', onPressed: _save),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 15, color: AppColors.primary),
        const SizedBox(width: 6),
        Text(label, style: AppTextStyles.labelMd),
      ],
    );
  }
}

class _StepperField extends StatelessWidget {
  const _StepperField({
    required this.valueLabel,
    required this.onDecrement,
    required this.onIncrement,
  });

  final String valueLabel;
  final VoidCallback? onDecrement;
  final VoidCallback? onIncrement;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(9999),
      ),
      child: Row(
        children: [
          _stepButton(Icons.remove, onDecrement),
          Expanded(
            child: Center(
              child: Text(valueLabel, style: AppTextStyles.labelLg),
            ),
          ),
          _stepButton(Icons.add, onIncrement),
        ],
      ),
    );
  }

  Widget _stepButton(IconData icon, VoidCallback? onTap) {
    return InkWell(
      onTap: onTap,
      customBorder: const CircleBorder(),
      child: Container(
        width: 32,
        height: 32,
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLowest,
          shape: BoxShape.circle,
          boxShadow: const [
            BoxShadow(color: AppColors.shadowAmbient, blurRadius: 1, offset: Offset(0, 1)),
          ],
        ),
        child: Icon(
          icon,
          size: 16,
          color: onTap == null ? AppColors.textMuted : AppColors.primary,
        ),
      ),
    );
  }
}
