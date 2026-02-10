# devian-tools — Project Archive


## Scope
- Devian 레포를 아카이브(zip)로 만들 때, 포함/제외 기준을 정리한다.


## Rules
- 빌드 산출물/캐시/대용량 아티팩트는 제외한다.
- 민감 정보(키/토큰/개인정보)는 제외한다.
- UPM 패키지와 UnityExample 미러는 "필요 시" 함께 포함한다. (요구 사항이 없으면 기본은 포함)


## Suggested Excludes
- Library/
- Temp/
- Obj/
- Build/
- Logs/
- .gradle/
- .idea/
- .vs/
- node_modules/


## Command Example
```bash
zip -r devian.zip . \
  -x "*/Library/*" \
  -x "*/Temp/*" \
  -x "*/Obj/*" \
  -x "*/Build/*" \
  -x "*/Logs/*" \
  -x "*/.gradle/*" \
  -x "*/.idea/*" \
  -x "*/.vs/*" \
  -x "*/node_modules/*"
```


## Notes
- 필요 시, Packages/ 또는 Assets/만 별도 아카이브로 분리하는 것도 가능하다.
