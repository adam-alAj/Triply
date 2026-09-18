import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_overview_data.dart';
import '../primary_button.dart';

/// Edit Item Modal (UI Pages §6). Returns the edited item, or null if the
/// user cancelled. Saving always marks the item user-modified — that's the
/// whole point of this surface per the task's acceptance criteria.
Future<ItineraryItemData?> showEditItemModal(
  BuildContext context,
  ItineraryItemData item,
) {
  return showDialog<ItineraryItemData>(
    context: context,
    builder: (_) => _EditItemModal(item: item),
  );
}

class _EditItemModal extends StatefulWidget {
  const _EditItemModal({required this.item});

  final ItineraryItemData item;

  @override
  State<_EditItemModal> createState() => _EditItemModalState();
}

class _EditItemModalState extends State<_EditItemModal> {
  late String _timeSlot = widget.item.timeSlot;
  late final _costController =
      TextEditingController(text: widget.item.estimatedCostLabel);
  late final _notesController =
      TextEditingController(text: widget.item.notes ?? '');

  static const _timeSlots = ['MORNING', 'AFTERNOON', 'EVENING'];

  @override
  void dispose() {
    _costController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  void _save() {
    Navigator.of(context).pop(
      widget.item.copyWith(
        timeSlot: _timeSlot,
        estimatedCostLabel: _costController.text.trim(),
        notes: _notesController.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Edit Activity', style: AppTextStyles.headlineSm),
            const SizedBox(height: 4),
            Text(widget.item.placeName, style: AppTextStyles.bodySm),
            const SizedBox(height: 16),
            Text('Time of day', style: AppTextStyles.labelMd),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              children: [
                for (final slot in _timeSlots)
                  ChoiceChip(
                    label: Text(_label(slot)),
                    selected: _timeSlot == slot,
                    onSelected: (_) => setState(() => _timeSlot = slot),
                    selectedColor: AppColors.primaryContainerLight,
                    labelStyle: AppTextStyles.labelSm.copyWith(
                      color: _timeSlot == slot
                          ? AppColors.onPrimaryContainerLight
                          : AppColors.onSurface,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 16),
            Text('Estimated cost', style: AppTextStyles.labelMd),
            const SizedBox(height: 8),
            TextField(
              controller: _costController,
              decoration: const InputDecoration(
                isDense: true,
                border: OutlineInputBorder(),
                hintText: 'e.g. Est. \$15',
              ),
            ),
            const SizedBox(height: 16),
            Text('Notes', style: AppTextStyles.labelMd),
            const SizedBox(height: 8),
            TextField(
              controller: _notesController,
              maxLines: 3,
              decoration: const InputDecoration(
                isDense: true,
                border: OutlineInputBorder(),
                hintText: 'Add a note for this activity...',
              ),
            ),
            const SizedBox(height: 20),
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
    );
  }

  String _label(String slot) => switch (slot) {
        'MORNING' => 'Morning',
        'AFTERNOON' => 'Afternoon',
        'EVENING' => 'Evening',
        _ => slot,
      };
}
