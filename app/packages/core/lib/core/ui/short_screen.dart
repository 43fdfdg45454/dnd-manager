import 'package:flutter/widgets.dart';

/// Height (logical pixels) under which a screen counts as short: a phone in
/// landscape, where the fixed parts of a page would leave no room for its
/// content.
const shortScreenHeight = 500.0;

/// Whether the screen is short ([shortScreenHeight]): the parts of a page that
/// stay fixed in portrait scroll away with the content and come back as soon
/// as the user scrolls up a little.
bool isShortScreen(BuildContext context) => MediaQuery.sizeOf(context).height < shortScreenHeight;
