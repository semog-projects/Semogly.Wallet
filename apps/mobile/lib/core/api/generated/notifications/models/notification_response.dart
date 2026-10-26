// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'notification_status.dart';
import 'notification_type.dart';

part 'notification_response.g.dart';

@JsonSerializable()
class NotificationResponse {
  const NotificationResponse({
    required this.id,
    required this.type,
    required this.recipient,
    required this.subject,
    required this.body,
    required this.status,
    required this.createdAt,
    required this.sentAt,
  });
  
  factory NotificationResponse.fromJson(Map<String, Object?> json) => _$NotificationResponseFromJson(json);
  
  final String id;
  final NotificationType type;
  final String recipient;
  final String subject;
  final String body;
  final NotificationStatus status;
  final DateTime createdAt;
  final DateTime? sentAt;

  Map<String, Object?> toJson() => _$NotificationResponseToJson(this);
}
