docker-image:
	docker build -t srclib/srclib-csharp .

release: docker-image
	docker push srclib/srclib-csharp
