// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'notification_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

NotificationResponse _$NotificationResponseFromJson(
  Map<String, dynamic> json,
) => NotificationResponse(
  id: json['id'] as String,
  type: $enumDecode(_$NotificationTypeEnumMap, json['type']),
  recipient: json['recipient'] as String,
  subject: json['subject'] as String,
  body: json['body'] as String,
  status: $enumDecode(_$NotificationStatusEnumMap, json['status']),
  createdAt: DateTime.parse(json['createdAt'] as String),
  sentAt: json['sentAt'] == null
      ? null
      : DateTime.parse(json['sentAt'] as String),
);

Map<String, dynamic> _$NotificationResponseToJson(
  NotificationResponse instance,
) => <String, dynamic>{
  'id': instance.id,
  'type': instance.type,
  'recipient': instance.recipient,
  'subject': instance.subject,
  'body': instance.body,
  'status': instance.status,
  'createdAt': instance.createdAt.toIso8601String(),
  'sentAt': instance.sentAt?.toIso8601String(),
};

const _$NotificationTypeEnumMap = {
  NotificationType.welcome: 'Welcome',
  NotificationType.transferSent: 'TransferSent',
  NotificationType.transferReceived: 'TransferReceived',
  NotificationType.transferFailed: 'TransferFailed',
};

const _$NotificationStatusEnumMap = {
  NotificationStatus.pending: 'Pending',
  NotificationStatus.sent: 'Sent',
};
