#!/usr/bin/env bash
# SDK build on Linux; CI signs with an ephemeral test key, never our release key.
set -euo pipefail
cd "$(dirname "$0")/.."
SDK="${ANDROID_HOME:?Android SDK required}"
BT="$SDK/build-tools/36.0.0"
PLATFORM="$SDK/platforms/android-36/android.jar"
python3 tools/prepare-android.py
mkdir -p android/build/{classes,tests,dex,instrumentation/classes,instrumentation/dex} dist verification/android
mapfile -t sources < <(find android/src -name '*.java')
javac -encoding UTF-8 -source 8 -target 8 -cp "$PLATFORM" -d android/build/classes "${sources[@]}"
javac -encoding UTF-8 -d android/build/tests android/src/com/jakedaxon/cybergolden/PetState.java android/tests/PetStateTest.java
java -cp android/build/tests com.jakedaxon.cybergolden.PetStateTest | tee verification/android/behavior-test.log
"$BT/aapt2" compile --dir android/res -o android/build/resources.zip
"$BT/aapt2" link -o android/build/base.apk -I "$PLATFORM" --manifest android/AndroidManifest.xml -A android/assets -0 wav android/build/resources.zip
python3 tools/finalize-android-zip.py android/build/base.apk
mapfile -t classes < <(find android/build/classes -name '*.class')
"$BT/d8" --release --min-api 26 --lib "$PLATFORM" --output android/build/dex "${classes[@]}"
jar uf android/build/base.apk -C android/build/dex classes.dex
"$BT/zipalign" -f -p 4 android/build/base.apk android/build/aligned.apk
keytool -genkeypair -keystore android/build/ci-test.jks -storepass ci-test-only -keypass ci-test-only -alias test -keyalg RSA -validity 30 -dname 'CN=CI test only' -noprompt
"$BT/apksigner" sign --ks android/build/ci-test.jks --ks-pass pass:ci-test-only --out android/build/ci-app.apk android/build/aligned.apk
"$BT/apksigner" verify --verbose android/build/ci-app.apk
javac -encoding UTF-8 -source 8 -target 8 -cp "$PLATFORM:android/build/classes" -d android/build/instrumentation/classes android/tests/instrumentation/AndroidTestRunner.java
"$BT/aapt2" link -o android/build/instrumentation/base.apk -I "$PLATFORM" --manifest android/tests/instrumentation/AndroidManifest.xml
mapfile -t tests < <(find android/build/instrumentation/classes -name '*.class')
"$BT/d8" --min-api 26 --lib "$PLATFORM" --classpath android/build/classes --output android/build/instrumentation/dex "${tests[@]}"
jar uf android/build/instrumentation/base.apk -C android/build/instrumentation/dex classes.dex
"$BT/zipalign" -f 4 android/build/instrumentation/base.apk android/build/instrumentation/aligned.apk
"$BT/apksigner" sign --ks android/build/ci-test.jks --ks-pass pass:ci-test-only --out android/build/instrumentation/tests.apk android/build/instrumentation/aligned.apk
