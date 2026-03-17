cd ../../tools

protoc --csharp_out=../lgbf/hub  --proto_path=../lgbf/underlying  ../lgbf/underlying/underlying.proto
protoc --csharp_out=../gem/unity/Assets/Script/ServerSDK  --proto_path=../lgbf/underlying  ../lgbf/underlying/underlying.proto