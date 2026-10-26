// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'daily_usage_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DailyUsageResponse _$DailyUsageResponseFromJson(Map<String, dynamic> json) =>
    DailyUsageResponse(
      accountId: json['accountId'] as String,
      date: DateTime.parse(json['date'] as String),
      used: (json['used'] as num).toDouble(),
      limit: (json['limit'] as num).toDouble(),
      remaining: (json['remaining'] as num).toDouble(),
    );

Map<String, dynamic> _$DailyUsageResponseToJson(DailyUsageResponse instance) =>
    <String, dynamic>{
      'accountId': instance.accountId,
      'date': instance.date.toIso8601String(),
      'used': instance.used,
      'limit': instance.limit,
      'remaining': instance.remaining,
    };
