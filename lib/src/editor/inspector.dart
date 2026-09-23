import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_scene/orblit_scene.dart' show EntityPath, PrefabState;
import 'package:orblit_weather/orblit_weather.dart';
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'colour.dart';
import 'commands.dart';
import 'history.dart';
import 'registry.dart';
import 'scene.dart';
import 'workspace.dart';

part 'inspector_banners.dart';
part 'inspector_scene.dart';
part 'inspector_rows.dart';
part 'inspector_light.dart';
part 'inspector_weather.dart';
part 'inspector_sections.dart';

/// Properties of whatever is selected.
///
/// The fields shown depend on what the thing is, which is the whole point of
/// components: a light and a mesh are not the same object with some fields
/// greyed out, they are different sets of components on an entity.
///
/// Every field runs a command. Nothing here writes to the scene directly, so
/// there is no edit that undo does not know about.
///
/// Which fields come in which order is [sections], and the inspector keeps no
/// list of its own: a section registered from outside is stacked, scrolled
/// and rebuilt exactly as the built-in ones are.
class Inspector extends StatelessWidget {
  const Inspector({
    super.key,
    required this.entry,
    required this.object,
    required this.history,
    required this.onLoad,
    this.selectionCount = 0,
    this.onApplyPrefab,
    this.onRevertPrefab,
    this.onUnpackPrefab,
    this.dataAsset,
    this.dataPanel,
    this.onOpenData,
    this.onDetachData,
    this.onOpenInterface,
    this.sections,
  });

  /// The scene being looked at, which need not be the loaded one — a scene can
  /// be inspected before it is opened.
  final SceneEntry? entry;

  /// The object selected, or null when the scene itself is.
  final SceneObject? object;

  final History history;

  final ValueChanged<SceneEntry> onLoad;

  /// How many objects are selected. The fields below edit one of them, and
  /// saying which beats leaving somebody to guess why their changes only
  /// landed on one thing.
  final int selectionCount;

  /// What the prefab band does, when there is one. Null in a context that has
  /// no project to write to — a test, or a scene inspected before it is open.
  final ValueChanged<String>? onApplyPrefab;
  final ValueChanged<String>? onRevertPrefab;
  final ValueChanged<String>? onUnpackPrefab;

  /// A data object selected in the project browser, which the inspector shows
  /// instead of the scene's selection.
  ///
  /// Instead rather than as well: two things claiming the same panel is how a
  /// panel starts needing tabs, and what somebody clicked last is what they
  /// are looking at.
  final String? dataAsset;

  /// The editor for [dataAsset]. Built by the shell, which owns the store.
  final Widget? dataPanel;

  /// Shows one of the selected object's data objects in the browser.
  final ValueChanged<String>? onOpenData;

  /// Takes one off the selected object.
  final void Function(String id, String path)? onDetachData;

  /// Opens the interface a canvas object shows.
  final ValueChanged<String>? onOpenInterface;

  /// The sections an object's fields come in, first to last, each shown
  /// only for the objects it applies to.
  ///
  /// Null is [InspectorSection.builtIn]. The shell passes its registry,
  /// which adds the shape and geometry controls: those are built by the
  /// shell, which owns what is being edited, and the inspector does not know
  /// what an extrude is.
  final List<InspectorSection>? sections;

  @override
  Widget build(BuildContext context) {
    final entry = this.entry;
    final selected = object;

    return Container(
      width: 296,
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(left: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            height: 32,
            padding: const EdgeInsets.symmetric(horizontal: Space.md),
            alignment: Alignment.centerLeft,
            decoration: const BoxDecoration(
              border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
            ),
            child: Text('INSPECTOR', style: OrblitText.section),
          ),
          Expanded(
            child:
                dataPanel ??
                (entry == null
                    ? Center(
                        child: Text(
                          'No scene loaded.',
                          style: OrblitText.caption,
                        ),
                      )
                    : (selected == null
                          ? _SceneFields(
                              key: ValueKey('scene/${entry.id}'),
                              entry: entry,
                              history: history,
                              onLoad: onLoad,
                            )
                          : Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                if (selectionCount > 1)
                                  _MultipleNotice(
                                    count: selectionCount,
                                    name: selected.name,
                                  ),
                                if (_instanceHolding(entry.scene!, selected)
                                    case final instance?)
                                  _PrefabBand(
                                    source: instance.prefab!.asset ?? '',
                                    readable:
                                        instance.prefab!.state ==
                                        PrefabState.open,
                                    partOf: identical(instance, selected)
                                        ? null
                                        : instance.name,
                                    onApply: onApplyPrefab == null
                                        ? null
                                        : () => onApplyPrefab!(selected.id),
                                    onRevert: onRevertPrefab == null
                                        ? null
                                        : () => onRevertPrefab!(selected.id),
                                    onUnpack: onUnpackPrefab == null
                                        ? null
                                        : () => onUnpackPrefab!(selected.id),
                                  ),
                                Expanded(
                                  child: _Fields(
                                    key: ValueKey(selected.id),
                                    sceneId: entry.id,
                                    scene: entry.scene!,
                                    object: selected,
                                    history: history,
                                    onOpenData: onOpenData,
                                    onDetachData: onDetachData,
                                    onOpenInterface: onOpenInterface,
                                    sections:
                                        sections ?? InspectorSection.builtIn,
                                  ),
                                ),
                              ],
                            ))),
          ),
        ],
      ),
    );
  }
}

class _Fields extends StatelessWidget {
  const _Fields({
    super.key,
    required this.sceneId,
    required this.scene,
    required this.object,
    required this.history,
    this.onOpenData,
    this.onDetachData,
    this.onOpenInterface,
    required this.sections,
  });

  final String sceneId;

  final EditorScene scene;

  final SceneObject object;

  final History history;

  final ValueChanged<String>? onOpenData;

  final void Function(String id, String path)? onDetachData;

  final ValueChanged<String>? onOpenInterface;

  final List<InspectorSection> sections;

  @override
  Widget build(BuildContext context) {
    final parent = object.parentId == null ? null : scene[object.parentId!];
    final target = InspectorTarget(
      sceneId: sceneId,
      scene: scene,
      object: object,
      history: history,
      onOpenData: onOpenData,
      onDetachData: onDetachData,
      onOpenInterface: onOpenInterface,
    );

    return ListView(
      padding: const EdgeInsets.symmetric(vertical: Space.sm),
      children: [
        _Header(
          name: scene.displayNameOf(object),
          icon: scene.displayIconOf(object),
          onRename: (value) {
            if (value == object.name) return;
            history.run(
              Rename(
                sceneId: sceneId,
                id: object.id,
                from: object.name,
                to: value,
              ),
            );
          },
          onRenameDone: history.seal,
        ),
        if (parent != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(Space.md, 0, Space.md, Space.sm),
            child: Row(
              children: [
                const Icon(
                  Icons.subdirectory_arrow_right,
                  size: 12,
                  color: OrblitColors.inkDim,
                ),
                const SizedBox(width: Space.xs),
                Flexible(
                  child: Text(
                    'in ${parent.name}',
                    overflow: TextOverflow.ellipsis,
                    style: OrblitText.caption.copyWith(fontSize: 11.5),
                  ),
                ),
              ],
            ),
          ),
        for (final section in sections)
          if (section.appliesTo(target)) section.build(target),
      ],
    );
  }
}

// The built-in sections. Written against what a section is given rather
// than against the widget showing them, so each is registered the same way
// a section from outside would be.
extension _Sections on InspectorTarget {
  /// Which interface this canvas puts on screen.
  ///
  /// A reference and not a copy, which is the same rule as a mesh and a data
  /// object: the `.oui` is the document, and two scenes showing the same one
  /// both change when it changes.
  Widget _interface() {
    final shown = object.interfaceAsset;

    return OrblitSection(
      title: 'Interface',
      icon: Icons.web_asset,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (shown == null)
            Text(
              'Nothing yet. Drag a .oui from the project onto the viewport.',
              style: OrblitText.caption.copyWith(fontSize: 11),
            )
          else ...[
            Row(
              children: [
                const Icon(
                  Icons.web_asset,
                  size: 13,
                  color: OrblitColors.inkDim,
                ),
                const SizedBox(width: Space.sm),
                Expanded(
                  child: Tooltip(
                    message: shown,
                    child: GestureDetector(
                      onTap: onOpenInterface == null
                          ? null
                          : () => onOpenInterface!(shown),
                      child: Text(
                        p.basename(shown),
                        overflow: TextOverflow.ellipsis,
                        style: OrblitText.label.copyWith(
                          color: OrblitColors.ink,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: Space.xs),
            Text(
              'Drawn over the scene. Hidden here hides it in the game too.',
              style: OrblitText.caption.copyWith(fontSize: 11),
            ),
          ],
        ],
      ),
    );
  }

  /// The data objects this one takes its settings from.
  ///
  /// Listed rather than inlined: the values belong to the file, and showing
  /// them here as though they were this object's own would invite somebody to
  /// change one and be surprised when thirty-nine other objects changed with
  /// it. The link is what this object owns; the values are edited where they
  /// live, one click away.
  Widget _data() {
    return OrblitSection(
      title: 'Data',
      icon: Icons.dataset_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final path in object.data)
            _DataLink(
              path: path,
              onOpen: onOpenData == null ? null : () => onOpenData!(path),
              onRemove: onDetachData == null
                  ? null
                  : () => onDetachData!(object.id, path),
            ),
          const SizedBox(height: Space.xs),
          Text(
            'Shared. Changing one of these changes it everywhere it is used.',
            style: OrblitText.caption.copyWith(fontSize: 11),
          ),
        ],
      ),
    );
  }

  Widget _visibility(EditorScene scene) {
    // Hidden by something further up is a different state from hidden here,
    // and an object that says "shown" while nothing appears is worse than no
    // control at all.
    final hiddenAbove = object.visible && !scene.isShown(object.id);

    return OrblitSection(
      title: 'Object',
      icon: Icons.visibility_outlined,
      child: Column(
        children: [
          ChoiceRow(
            label: 'Visible',
            options: const ['Hidden', 'Shown'],
            selected: object.visible ? 'Shown' : 'Hidden',
            onSelect: (value) {
              final wanted = value == 'Shown';
              if (wanted == object.visible) return;
              history
                ..run(
                  SetVisible(
                    sceneId: sceneId,
                    id: object.id,
                    name: object.name,
                    to: wanted,
                  ),
                )
                ..seal();
            },
          ),
          if (hiddenAbove)
            Padding(
              padding: const EdgeInsets.fromLTRB(
                Space.md,
                Space.xs,
                Space.md,
                0,
              ),
              child: Text(
                'Hidden anyway, because something it is inside is hidden.',
                style: OrblitText.caption,
              ),
            ),
        ],
      ),
    );
  }

  Widget _transform() => OrblitSection(
    title: 'Transform',
    icon: Icons.open_with,
    child: Column(
      children: [
        VectorRow(
          label: 'Position',
          sceneId: sceneId,
          object: object,
          field: TransformField.position,
          history: history,
          step: 0.02,
        ),
        VectorRow(
          label: 'Rotation',
          sceneId: sceneId,
          object: object,
          field: TransformField.rotation,
          history: history,
          step: 0.5,
          decimals: 1,
        ),
        VectorRow(
          label: 'Scale',
          sceneId: sceneId,
          object: object,
          field: TransformField.scale,
          history: history,
          step: 0.02,
          minimum: 0.001,
        ),
      ],
    ),
  );

  Widget _mesh() => OrblitSection(
    title: 'Mesh renderer',
    icon: Icons.view_in_ar_outlined,
    child: Column(
      children: [
        ColourRow(
          label: 'Base colour',
          value: object.colour,
          onChanged: (value) => history
            ..run(
              SetColour(
                sceneId: sceneId,
                id: object.id,
                name: object.name,
                from: object.colour,
                to: value,
              ),
            )
            ..seal(),
        ),
        ChoiceRow(
          label: 'Cast shadows',
          options: const ['Off', 'On'],
          selected: object.castShadows ? 'On' : 'Off',
          onSelect: (value) {
            final wanted = value == 'On';
            if (wanted == object.castShadows) return;
            history
              ..run(
                SetCastShadows(
                  sceneId: sceneId,
                  id: object.id,
                  name: object.name,
                  to: wanted,
                ),
              )
              ..seal();
          },
        ),
        ChoiceRow(
          label: 'Receive shadows',
          options: const ['Off', 'On'],
          selected: object.receiveShadows ? 'On' : 'Off',
          onSelect: (value) {
            final wanted = value == 'On';
            if (wanted == object.receiveShadows) return;
            history
              ..run(
                SetReceiveShadows(
                  sceneId: sceneId,
                  id: object.id,
                  name: object.name,
                  to: wanted,
                ),
              )
              ..seal();
          },
        ),
        // Nought for anything rigid, which is almost everything — so the
        // row reads as off rather than as a number somebody has to
        // interpret. Only the weather decides how hard it blows; this is
        // how much this object answers.
        SliderRow(
          label: 'Sway',
          value: object.sway,
          min: 0,
          max: 1,
          onChanged: (value) => history.run(
            SetSway(
              sceneId: sceneId,
              id: object.id,
              name: object.name,
              from: object.sway,
              to: value,
            ),
          ),
          onSettled: history.seal,
        ),
        TextRow(label: 'Mesh', value: object.meshAsset ?? 'cube (built in)'),
        // Set by dropping a texture on the object, or opening one with the
        // object selected; only cleared from here. A picker would be the
        // obvious thing, and the browser is already a picker.
        FieldRow(
          label: 'Texture',
          child: Row(
            children: [
              Expanded(
                child: Container(
                  height: 24,
                  padding: const EdgeInsets.symmetric(horizontal: Space.sm),
                  alignment: Alignment.centerLeft,
                  decoration: BoxDecoration(
                    color: OrblitColors.raised,
                    borderRadius: BorderRadius.circular(4),
                  ),
                  child: Text(
                    object.materialAsset ?? "the mesh's own",
                    style: OrblitText.monoValue.copyWith(fontSize: 11),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ),
              if (object.materialAsset != null)
                GestureDetector(
                  onTap: () => history
                    ..run(
                      SetMaterialAsset(
                        sceneId: sceneId,
                        id: object.id,
                        name: object.name,
                        from: object.materialAsset,
                        to: null,
                      ),
                    )
                    ..seal(),
                  child: const Padding(
                    padding: EdgeInsets.only(left: Space.xs),
                    child: Icon(
                      Icons.close,
                      size: 14,
                      color: OrblitColors.inkDim,
                    ),
                  ),
                ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(Space.md, Space.xs, Space.md, 0),
          child: Text(
            'A ground plane that casts shadows casts them onto itself, '
            'which is most of what makes a scene look dirty.',
            style: OrblitText.caption,
          ),
        ),
      ],
    ),
  );
}

/// The instance [object] is, or the outermost one it is a part of — which is
/// the one a scene links to, and so the one the prefab band acts on.
SceneObject? _instanceHolding(EditorScene scene, SceneObject object) {
  final head = EntityPath.instanceOf(object.id);
  final instance = head == null ? object : scene[head];
  return instance?.prefab?.asset == null ? null : instance;
}
