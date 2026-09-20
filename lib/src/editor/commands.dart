import 'package:flutter/material.dart';
import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:orblit_weather/orblit_weather.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'boundary.dart';
import 'history.dart';
import 'scene.dart';
import 'scene_document.dart';
import 'surface.dart';

// Every edit the editor can make, one class each, grouped by what it edits.
// They are here rather than beside the panels that raise them because undo
// is the reason they exist: a command has to be able to put back what it
// changed without the panel that raised it still being open.

part 'commands_transform.dart';
part 'commands_appearance.dart';
part 'commands_light.dart';
part 'commands_hierarchy.dart';
part 'commands_links.dart';
part 'commands_scene.dart';
part 'commands_geometry.dart';
