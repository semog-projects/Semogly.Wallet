// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'daily_usage_response.g.dart';

@JsonSerializable()
class DailyUsageResponse {
  const DailyUsageResponse({
    required this.accountId,
    required this.date,
    required this.used,
    required this.limit,
    required this.remaining,
  });
  
  factory DailyUsageResponse.fromJson(Map<String, Object?> json) => _$DailyUsageResponseFromJson(json);
  
  final String accountId;
  final DateTime date;
  final double used;
  final double limit;
  final double remaining;

  Map<String, Object?> toJson() => _$DailyUsageResponseToJson(this);
}
