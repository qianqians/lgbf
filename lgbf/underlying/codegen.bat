cd ../../tools

protoc --csharp_out=../lgbf/hub  --proto_path=../lgbf/underlying  ../lgbf/underlying/underlying.proto
