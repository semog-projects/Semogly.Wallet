// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/notification_response.dart';

part 'notifications_client.g.dart';

@RestApi()
abstract class NotificationsClient {
  factory NotificationsClient(Dio dio, {String? baseUrl}) = _NotificationsClient;

  /// Lista as notificações enviadas para a conta, da mais recente para a mais antiga
  @GET('/notifications/accounts/{accountId}')
  Future<List<NotificationResponse>> getNotifications({
    @Path('accountId') required String accountId,
    @Query('limit') int? limit = 20,
  });
}
