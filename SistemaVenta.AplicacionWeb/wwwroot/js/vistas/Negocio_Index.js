$(document).ready(function () {

    $(".card-body").LoadingOverlay("show");

    fetch("/Negocio/Obtener")
        .then(response => {
            $(".card-body").LoadingOverlay("hide");
            return response.ok ? response.json() : Promise.reject(response);
        })
        .then(responseJson => {
            console.log("GET /Negocio/Obtener ->", responseJson);

            if (responseJson.estado) {
                const d = responseJson.objeto;

                $("#txtRfc").val(d.rfc);
                $("#txtRazonSocial").val(d.nombre);
                $("#txtCorreo").val(d.correo);
                $("#txtDireccion").val(d.direccion);
                $("#txTelefono").val(d.telefono);
                $("#txtCodigoPostal").val(d.codigoPostal);
                $("#txtSimboloMoneda").val(d.simboloMoneda);
                $("#cbRegimenFiscal").val(d.regimenFiscal);
                $("#imgLogo").attr("src", d.urlLogo);

            } else {
                swal("Lo sentimos", responseJson.mensaje, "error");
            }
        })
        .catch(err => {
            $(".card-body").LoadingOverlay("hide");
            console.error("Error fetch Obtener:", err);
            swal("Error", "No se pudo obtener datos del negocio. Revisa consola.", "error");
        });
});

$("#btnGuardarCambios").click(function () {

    // Incluimos select en la validación: ahora buscamos todos los campos con .input-validar (input, select, textarea)
    const inputs = $("input.input-validar, select.input-validar, textarea.input-validar").serializeArray();
    const inputs_sin_valor = inputs.filter((item) => item.value == null || item.value.toString().trim() == "");

    if (inputs_sin_valor.length > 0) {
        const mensaje = `Debe completar el campo: "${inputs_sin_valor[0].name}"`;
        toastr.warning("", mensaje);
        $(`[name="${inputs_sin_valor[0].name}"]`).focus();
        return;
    }

    // Construimos modelo (las claves pueden ser camelCase; JsonConvert.DeserializeObject es case-insensitive por defecto)
    const modelo = {
        rfc: $("#txtRfc").val(),
        nombre: $("#txtRazonSocial").val(),
        correo: $("#txtCorreo").val(),
        direccion: $("#txtDireccion").val(),
        telefono: $("#txTelefono").val(),
        codigoPostal: $("#txtCodigoPostal").val(),
        simboloMoneda: $("#txtSimboloMoneda").val(),
        regimenFiscal: $("#cbRegimenFiscal").val()
    };

    // -- DEBUG: logear modelo antes de enviar
    console.log("Modelo a enviar (JS):", modelo);

    // Verificar que cbRegimenFiscal exista y tenga valor
    if ($("#cbRegimenFiscal").length === 0) {
        console.error("No existe #cbRegimenFiscal en el DOM");
        swal("Error", "El select de Régimen Fiscal no existe en la vista.", "error");
        return;
    }
    if (!modelo.regimenFiscal || modelo.regimenFiscal.toString().trim() === "") {
        console.warn("regimenFiscal vacío:", modelo.regimenFiscal);
        // opcional: alerta o continuar según tu lógica
    }

    const inputLogo = document.getElementById("txtLogo");
    const formData = new FormData();

    if (inputLogo && inputLogo.files && inputLogo.files.length > 0) {
        formData.append("logo", inputLogo.files[0]);
    } else {
        // si no hay archivo, append null no conviene; omitimos
    }

    formData.append("modelo", JSON.stringify(modelo));

    // -- DEBUG: listar contenido de formData (solo para desarrollador; FormData no se puede console.log directamente)
    (async () => {
        for (let pair of formData.entries()) {
            console.log("formData entry:", pair[0], pair[1]);
        }
    })();

    $(".card-body").LoadingOverlay("show");

    fetch("/Negocio/GuardarCambios", {
        method: "POST",
        body: formData
    })
        .then(response => {
            $(".card-body").LoadingOverlay("hide");
            return response.ok ? response.json() : Promise.reject(response);
        })
        .then(responseJson => {
            console.log("POST /Negocio/GuardarCambios ->", responseJson);
            if (responseJson.estado) {
                const d = responseJson.objeto;
                $("#imgLogo").attr("src", d.urlLogo);
                // Actualizar campos en pantalla por si algo cambió
                $("#cbRegimenFiscal").val(d.regimenFiscal);
                toastr.success("", "Cambios guardados correctamente");
            } else {
                swal("Lo sentimos", responseJson.mensaje, "error");
            }
        })
        .catch(err => {
            $(".card-body").LoadingOverlay("hide");
            console.error("Error POST GuardarCambios:", err);
            swal("Error", "Ocurrió un error al guardar. Revisa consola.", "error");
        });
});
