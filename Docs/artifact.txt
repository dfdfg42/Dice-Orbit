유물(Artifact) 시스템

Artifact->
ScriptableObject인 ArtifactData와 ICombatReactor를 상속받은 일반 클래스인 RuntimeArtifact로 구성.
ArtifactData
멤버 변수 : 유물 이름, 유물 설명, 유물 아이콘
역할 : SO 형태로 유물의 겉값 밑 설명을 관리하고, 기획 담당자가 수정할 수 있게 함
RuntimeArtifact
특징 : 각 유물별로 RuntimeArtifact를 상속받은 새로운 클래스가 있음
멤버 변수/함수: ArtifactData, 각 유물 작동 로직(OnReact)
역할 : 유물의 실제 작동 관리

Artifact 관리->
ArtifactManager 및 ArtifactPanalUI 클래스(싱글톤)
ArtifactManager
AddArtifact, RemoveArtifact<RuntimeArtifact T>, ResetArtifact 등 구현되어 있음
Pipeline에서 ReActer 수집할 때 ArtifactManager에서도 수집->전투나 이동 등 파이프라인을 타는 행동에서 유물과 상호작용 가능
추가로 주사위 굴림 등을 파이프라인에 넣는다면 그런 것과도 상호작용 할 수 있을 듯
ArtifactPanalUI
유니티의 horizontal layout group을 이용해 슬더스식 좌상단에서 옆으로 유물 나열됨
현재 GameStatus에 따라 숨기는 기능이 **구현되어 있지 않음.** 어느 페이즈에 숨기고 보여줄지 결정하면 그때 추가 필요해 보임
