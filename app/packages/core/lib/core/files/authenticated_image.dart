import 'dart:async';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart' show SynchronousFuture;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'file_disk_cache.dart';

/// An image of a stored file. It is loaded through [FileBytesLoader] (the
/// authenticated Dio client plus a disk cache), so it carries the bearer token
/// and honours pinned certificates, which `Image.network` could not do.
@immutable
class AuthenticatedFileImage extends ImageProvider<AuthenticatedFileImage> {
  const AuthenticatedFileImage(this.url, this.loader);

  /// Relative URL (`/api/v1/files/{id}`) of the file.
  final String url;
  final FileBytesLoader loader;

  @override
  Future<AuthenticatedFileImage> obtainKey(ImageConfiguration configuration) =>
      SynchronousFuture(this);

  @override
  ImageStreamCompleter loadImage(AuthenticatedFileImage key, ImageDecoderCallback decode) {
    return MultiFrameImageStreamCompleter(codec: _load(key, decode), scale: 1, debugLabel: key.url);
  }

  Future<ui.Codec> _load(AuthenticatedFileImage key, ImageDecoderCallback decode) async {
    final bytes = await loader(key.url);
    if (bytes.isEmpty) throw StateError('Empty image: ${key.url}');
    final buffer = await ui.ImmutableBuffer.fromUint8List(bytes);
    return decode(buffer);
  }

  // The loader is not part of the identity: the same URL is the same image.
  @override
  bool operator ==(Object other) => other is AuthenticatedFileImage && other.url == url;

  @override
  int get hashCode => url.hashCode;

  @override
  String toString() => 'AuthenticatedFileImage($url)';
}

/// Shows the stored file at [url] with a placeholder while it loads and an
/// error state with a retry button. If the token expires mid-load the request
/// is retried by the HTTP layer; any remaining failure can be retried by hand.
class AuthenticatedImage extends ConsumerStatefulWidget {
  const AuthenticatedImage({
    super.key,
    required this.url,
    this.fit = BoxFit.cover,
    this.width,
    this.height,
    this.compact = false,
  });

  final String url;
  final BoxFit fit;
  final double? width;
  final double? height;

  /// Small error state (an icon only), for thumbnails.
  final bool compact;

  @override
  ConsumerState<AuthenticatedImage> createState() => _AuthenticatedImageState();
}

class _AuthenticatedImageState extends ConsumerState<AuthenticatedImage> {
  int _attempt = 0;

  Future<void> _retry(ImageProvider provider) async {
    await provider.evict();
    if (mounted) setState(() => _attempt++);
  }

  @override
  Widget build(BuildContext context) {
    final provider = AuthenticatedFileImage(widget.url, ref.watch(fileBytesLoaderProvider));
    return SizedBox(
      width: widget.width,
      height: widget.height,
      child: Image(
        key: ValueKey('${widget.url}#$_attempt'),
        image: provider,
        fit: widget.fit,
        width: widget.width,
        height: widget.height,
        frameBuilder: (context, child, frame, wasSynchronouslyLoaded) {
          if (wasSynchronouslyLoaded || frame != null) return child;
          return const _ImagePlaceholder();
        },
        errorBuilder: (context, error, stackTrace) =>
            _ImageError(compact: widget.compact, onRetry: () => _retry(provider)),
      ),
    );
  }
}

/// A still placeholder: it keeps the layout without an endless animation.
class _ImagePlaceholder extends StatelessWidget {
  const _ImagePlaceholder();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return ColoredBox(
      key: const Key('image-loading'),
      color: scheme.surfaceContainerHighest,
      child: Center(child: Icon(Icons.image_outlined, color: scheme.onSurfaceVariant)),
    );
  }
}

class _ImageError extends StatelessWidget {
  const _ImageError({required this.compact, required this.onRetry});

  final bool compact;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return InkWell(
      key: const Key('image-retry'),
      onTap: onRetry,
      child: ColoredBox(
        color: scheme.surfaceContainerHighest,
        child: Center(
          child: compact
              ? Icon(Icons.refresh, color: scheme.onSurfaceVariant)
              : Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.broken_image_outlined, color: scheme.onSurfaceVariant),
                    const SizedBox(height: 4),
                    Text(
                      'No se pudo cargar. Toca para reintentar',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
        ),
      ),
    );
  }
}
