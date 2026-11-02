import QtQuick
import com.corsair.cue.style
QtObject {
    // The existing Appearance plugin updates this palette probe on live theme changes.
    property color themeProbe: CuePalette.layer3
    readonly property bool light: themeProbe.hslLightness > 0.5
    readonly property color layer1: light ? "#fbfbfc" : "#242427"
    readonly property color layer3: light ? "#fbfbfc" : "#242427"
    readonly property color layer5: light ? "#eeeeF1" : "#333338"
    readonly property color layer6: light ? "#e5e5e9" : "#45454b"
    readonly property color layer7: light ? "#777780" : "#a4a4ae"
    readonly property color layer8: light ? "#51515b" : "#d4d4dc"
    readonly property color layer9: light ? "#202027" : "#f8f8fc"
    readonly property color hover1: light ? "#eeeeF1" : "#333338"
    readonly property real hover1opacity: 1
}
